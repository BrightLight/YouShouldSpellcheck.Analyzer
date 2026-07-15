namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Concurrent;
  using System.Collections.Generic;
  using System.Collections.Immutable;
  using System.IO;
  using System.Linq;
  using System.Text;
  using System.Threading;
  using System.Xml.Serialization;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.Diagnostics;
  using Microsoft.CodeAnalysis.Text;
  using WeCantSpell.Hunspell;

  /// <summary>
  /// Holds all mutable caches and immutable inputs for one analyzer compilation.
  /// </summary>
  internal sealed class CompilationSpellcheckState
  {
    private const string SettingsFileName = "youshouldspellcheck.config.xml";
    private const string CustomDictionaryPrefix = "CustomDictionary.";

    private readonly ImmutableDictionary<string, DictionarySources> dictionarySources;
    private readonly ImmutableDictionary<string, ImmutableHashSet<string>> customWords;
    private readonly ConcurrentDictionary<string, Lazy<WordList?>> dictionaries = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Tuple<string, string>, bool> spellingCache = new();
    private readonly ConcurrentQueue<LanguageToolCandidate> languageToolCandidates = new();

    private CompilationSpellcheckState(
      ISpellcheckSettings settings,
      ImmutableDictionary<string, DictionarySources> dictionarySources,
      ImmutableDictionary<string, ImmutableHashSet<string>> customWords)
    {
      this.Settings = settings;
      this.dictionarySources = dictionarySources;
      this.customWords = customWords;
    }

    public ISpellcheckSettings Settings { get; }

    public bool LanguageToolEnabled =>
      this.Settings.LanguageToolMode == LanguageToolExecutionMode.CompilationEnd
      && !string.IsNullOrWhiteSpace(this.Settings.LanguageToolUrl);

    public static CompilationSpellcheckState Create(AnalyzerOptions options, CancellationToken cancellationToken)
    {
      var additionalFiles = options?.AdditionalFiles ?? ImmutableArray<AdditionalText>.Empty;
      var settings = ReadSettings(additionalFiles, cancellationToken) ?? new DefaultSpellcheckSettings();
      var sources = ReadDictionarySources(additionalFiles, cancellationToken);
      var customWords = ReadCustomWords(additionalFiles, cancellationToken);
      return new CompilationSpellcheckState(settings, sources, customWords);
    }

    public IEnumerable<ILanguage> LanguagesByRule(string ruleId)
    {
      switch (ruleId)
      {
        case ClassNameSpellcheckAnalyzer.ClassNameDiagnosticId:
          return this.Settings.ClassNameLanguages;
        case MethodNameSpellcheckAnalyzer.MethodNameDiagnosticId:
          return this.Settings.MethodNameLanguages;
        case VariableNameSpellcheckAnalyzer.VariableNameDiagnosticId:
          return this.Settings.VariableNameLanguages;
        case PropertyNameSpellcheckAnalyzer.PropertyNameDiagnosticId:
          return this.Settings.PropertyNameLanguages;
        case XmlTextSpellcheckAnalyzer.CommentDiagnosticId:
          return this.Settings.CommentLanguages;
        case StringLiteralSpellcheckAnalyzer.StringLiteralDiagnosticId:
          return this.Settings.StringLiteralLanguages;
        case EnumNameSpellcheckAnalyzer.EnumNameDiagnosticId:
          return this.Settings.EnumNameLanguages;
        case EnumMemberNameSpellcheckAnalyzer.EnumMemberNameDiagnosticId:
          return this.Settings.EnumMemberNameLanguages;
        case EventNameSpellcheckAnalyzer.EventNameDiagnosticId:
          return this.Settings.EventNameLanguages;
        default:
          return this.Settings.DefaultLanguages;
      }
    }

    public bool IsWordCorrect(string word, string language)
    {
      if (string.IsNullOrEmpty(language))
      {
        return true;
      }

      var key = Tuple.Create(language, word);
      return this.spellingCache.GetOrAdd(key, _ => this.CheckWordCore(word, language));
    }

    public IEnumerable<string> Suggest(string word, string language)
    {
      if (!this.dictionarySources.ContainsKey(language))
      {
        return Enumerable.Empty<string>();
      }

      var dictionary = this.dictionaries.GetOrAdd(
        language,
        key => new Lazy<WordList?>(() => this.CreateDictionary(key), LazyThreadSafetyMode.ExecutionAndPublication));
      return dictionary.Value?.Suggest(word) ?? Enumerable.Empty<string>();
    }

    public bool QueueLanguageToolText(string text, Location location, IEnumerable<ILanguage> languages)
    {
      if (!this.LanguageToolEnabled || string.IsNullOrWhiteSpace(text))
      {
        return false;
      }

      var languageCodes = languages
        .Select(language => language.LanguageToolLanguage)
        .Where(language => !string.IsNullOrWhiteSpace(language))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToImmutableArray();
      if (languageCodes.IsDefaultOrEmpty)
      {
        return false;
      }

      this.languageToolCandidates.Enqueue(new LanguageToolCandidate(text, location, languageCodes));
      return true;
    }

    public ImmutableArray<LanguageToolCandidate> GetLanguageToolCandidates() =>
      this.languageToolCandidates.ToImmutableArray();

    private static ISpellcheckSettings? ReadSettings(ImmutableArray<AdditionalText> additionalFiles, CancellationToken cancellationToken)
    {
      var settingsFile = additionalFiles.FirstOrDefault(file =>
        string.Equals(Path.GetFileName(file.Path), SettingsFileName, StringComparison.OrdinalIgnoreCase));
      var settingsText = settingsFile?.GetText(cancellationToken);
      if (settingsText == null)
      {
        return null;
      }

      try
      {
        var serializer = new XmlSerializer(typeof(SpellcheckSettings));
        using var reader = new StringReader(settingsText.ToString());
        return serializer.Deserialize(reader) is SpellcheckSettings settings
          ? new SpellcheckSettingsWrapper(settings, settingsFile!.Path)
          : null;
      }
      catch (InvalidOperationException)
      {
        // Configuration diagnostics are handled in a later validation increment.
        return null;
      }
    }

    private static ImmutableDictionary<string, DictionarySources> ReadDictionarySources(
      ImmutableArray<AdditionalText> additionalFiles,
      CancellationToken cancellationToken)
    {
      var filesByPath = additionalFiles.ToImmutableDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
      var builder = ImmutableDictionary.CreateBuilder<string, DictionarySources>(StringComparer.OrdinalIgnoreCase);
      foreach (var dictionaryFile in additionalFiles.Where(file => string.Equals(Path.GetExtension(file.Path), ".dic", StringComparison.OrdinalIgnoreCase)))
      {
        cancellationToken.ThrowIfCancellationRequested();
        var affixPath = Path.ChangeExtension(dictionaryFile.Path, ".aff");
        if (!filesByPath.TryGetValue(affixPath, out var affixFile))
        {
          continue;
        }

        var dictionaryText = dictionaryFile.GetText(cancellationToken);
        var affixText = affixFile.GetText(cancellationToken);
        if (dictionaryText != null && affixText != null)
        {
          builder[Path.GetFileNameWithoutExtension(dictionaryFile.Path)] = new DictionarySources(dictionaryText, affixText);
        }
      }

      return builder.ToImmutable();
    }

    private static ImmutableDictionary<string, ImmutableHashSet<string>> ReadCustomWords(
      ImmutableArray<AdditionalText> additionalFiles,
      CancellationToken cancellationToken)
    {
      var builder = ImmutableDictionary.CreateBuilder<string, ImmutableHashSet<string>>(StringComparer.OrdinalIgnoreCase);
      foreach (var file in additionalFiles)
      {
        var fileName = Path.GetFileName(file.Path);
        if (!fileName.StartsWith(CustomDictionaryPrefix, StringComparison.OrdinalIgnoreCase)
          || !fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
          continue;
        }

        var language = fileName.Substring(CustomDictionaryPrefix.Length, fileName.Length - CustomDictionaryPrefix.Length - ".txt".Length);
        var text = file.GetText(cancellationToken);
        if (text != null)
        {
          builder[language] = text.Lines
            .Select(line => line.ToString().Trim())
            .Where(line => line.Length > 0)
            .ToImmutableHashSet(StringComparer.Ordinal);
        }
      }

      return builder.ToImmutable();
    }

    private bool CheckWordCore(string word, string language)
    {
      if (this.customWords.TryGetValue(language, out var words) && words.Contains(word))
      {
        return true;
      }

      // An unavailable language is an invalid input, not evidence that every word is misspelled.
      if (!this.dictionarySources.ContainsKey(language))
      {
        return true;
      }

      var dictionary = this.dictionaries.GetOrAdd(
        language,
        key => new Lazy<WordList?>(() => this.CreateDictionary(key), LazyThreadSafetyMode.ExecutionAndPublication));
      return dictionary.Value?.Check(word) ?? true;
    }

    private WordList? CreateDictionary(string language)
    {
      if (!this.dictionarySources.TryGetValue(language, out var sources))
      {
        return null;
      }

      using var dictionaryStream = GenerateStream(sources.Dictionary);
      using var affixStream = GenerateStream(sources.Affix);
      return WordList.CreateFromStreams(dictionaryStream, affixStream);
    }

    private static Stream GenerateStream(SourceText sourceText)
    {
      var stream = new MemoryStream();
      using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true))
      {
        sourceText.Write(writer);
      }

      stream.Position = 0;
      return stream;
    }

    private sealed class DictionarySources
    {
      public DictionarySources(SourceText dictionary, SourceText affix)
      {
        this.Dictionary = dictionary;
        this.Affix = affix;
      }

      public SourceText Dictionary { get; }

      public SourceText Affix { get; }
    }
  }

  internal sealed class LanguageToolCandidate
  {
    public LanguageToolCandidate(string text, Location location, ImmutableArray<string> languages)
    {
      this.Text = text;
      this.Location = location;
      this.Languages = languages;
    }

    public string Text { get; }

    public Location Location { get; }

    public ImmutableArray<string> Languages { get; }
  }
}
