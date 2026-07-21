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
    private readonly ConcurrentQueue<Diagnostic> languageToolFallbackDiagnostics = new();

    private CompilationSpellcheckState(
      ISpellcheckSettings settings,
      LanguageToolExecutionMode languageToolMode,
      ImmutableDictionary<string, DictionarySources> dictionarySources,
      ImmutableDictionary<string, ImmutableHashSet<string>> customWords)
    {
      this.Settings = settings;
      this.LanguageToolMode = languageToolMode;
      this.dictionarySources = dictionarySources;
      this.customWords = customWords;
    }

    public ISpellcheckSettings Settings { get; }

    public LanguageToolExecutionMode LanguageToolMode { get; }

    public bool LanguageToolEnabled =>
      this.LanguageToolMode != LanguageToolExecutionMode.Off
      && !string.IsNullOrWhiteSpace(this.Settings.LanguageToolUrl);

    public bool LanguageToolAutoFallback => this.LanguageToolMode == LanguageToolExecutionMode.AutoFallback;

    public static CompilationSpellcheckState Create(AnalyzerOptions options, CancellationToken cancellationToken)
    {
      var additionalFiles = options?.AdditionalFiles ?? ImmutableArray<AdditionalText>.Empty;
      var xmlSettings = ReadSettings(additionalFiles, cancellationToken, out var settingsPath);
      var settings = ApplyMsBuildOverrides(xmlSettings ?? new SpellcheckSettings(), settingsPath, options);
      var languageToolMode = ReadLanguageToolModeOverride(options) ?? settings.LanguageToolMode;
      var sources = ReadDictionarySources(additionalFiles, cancellationToken);
      var customWords = ReadCustomWords(additionalFiles, cancellationToken);
      return new CompilationSpellcheckState(settings, languageToolMode, sources, customWords);
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

    public bool QueueLanguageToolText(
      string text,
      Location location,
      IEnumerable<ILanguage> languages,
      LanguageToolTextKind textKind,
      ImmutableArray<int> sourcePositions = default)
    {
      if (!this.LanguageToolEnabled || !this.IsLanguageToolTextKindEnabled(textKind) || string.IsNullOrWhiteSpace(text))
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

      this.languageToolCandidates.Enqueue(new LanguageToolCandidate(text, location, languageCodes, sourcePositions));
      return true;
    }

    public ImmutableArray<LanguageToolCandidate> GetLanguageToolCandidates() =>
      this.languageToolCandidates
        .Distinct(LanguageToolCandidateComparer.Instance)
        .ToImmutableArray();

    public void ReportOrDeferLocalDiagnostic(string ruleId, Diagnostic diagnostic, SyntaxNodeAnalysisContext context)
    {
      var isLanguageToolTextRule = ruleId == StringLiteralSpellcheckAnalyzer.AttributeArgumentStringDiagnosticId
        || ruleId == StringLiteralSpellcheckAnalyzer.StringLiteralDiagnosticId;
      var diagnosticLocation = diagnostic.Location;
      var hasQueuedCandidate = this.languageToolCandidates.Any(candidate =>
        Equals(candidate.Location.SourceTree, diagnosticLocation.SourceTree)
        && candidate.Location.SourceSpan.Contains(diagnosticLocation.SourceSpan));
      if (this.LanguageToolAutoFallback && isLanguageToolTextRule && hasQueuedCandidate)
      {
        this.languageToolFallbackDiagnostics.Enqueue(diagnostic);
      }
      else
      {
        context.ReportDiagnostic(diagnostic);
      }
    }

    public ImmutableArray<Diagnostic> GetLanguageToolFallbackDiagnostics() =>
      this.languageToolFallbackDiagnostics.ToImmutableArray();

    private bool IsLanguageToolTextKindEnabled(LanguageToolTextKind textKind) =>
      this.Settings.LanguageToolScope == LanguageToolScope.StringLiteralsAndAttributeArguments
      || (this.Settings.LanguageToolScope == LanguageToolScope.AttributeArgumentsOnly && textKind == LanguageToolTextKind.AttributeArgument)
      || (this.Settings.LanguageToolScope == LanguageToolScope.StringLiteralsOnly && textKind == LanguageToolTextKind.StringLiteral);

    private static LanguageToolExecutionMode? ReadLanguageToolModeOverride(AnalyzerOptions? options)
    {
      const string propertyName = "build_property.YouShouldSpellcheckLanguageToolMode";
      return options != null
        && options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(propertyName, out var value)
        && Enum.TryParse(value, ignoreCase: true, out LanguageToolExecutionMode mode)
          ? mode
          : null;
    }

    private static SpellcheckSettings? ReadSettings(
      ImmutableArray<AdditionalText> additionalFiles,
      CancellationToken cancellationToken,
      out string? settingsPath)
    {
      var settingsFile = additionalFiles.FirstOrDefault(file =>
        string.Equals(Path.GetFileName(file.Path), SettingsFileName, StringComparison.OrdinalIgnoreCase));
      settingsPath = settingsFile?.Path;
      var settingsText = settingsFile?.GetText(cancellationToken);
      if (settingsText == null)
      {
        return null;
      }

      try
      {
        var serializer = new XmlSerializer(typeof(SpellcheckSettings));
        using var reader = new StringReader(settingsText.ToString());
        return serializer.Deserialize(reader) as SpellcheckSettings;
      }
      catch (InvalidOperationException)
      {
        // Configuration diagnostics are handled in a later validation increment.
        return null;
      }
    }

    private static ISpellcheckSettings ApplyMsBuildOverrides(
      SpellcheckSettings settings,
      string? settingsPath,
      AnalyzerOptions? options)
    {
      var globalOptions = options?.AnalyzerConfigOptionsProvider.GlobalOptions;
      if (globalOptions == null)
      {
        return new SpellcheckSettingsWrapper(settings, settingsPath);
      }

      var dictionaryMappings = ReadMappings(globalOptions, "YouShouldSpellcheckDictionaryMappings");
      var languageToolMappings = ReadMappings(globalOptions, "YouShouldSpellcheckLanguageToolMappings");
      var overriddenSettings = new SpellcheckSettings
      {
        DefaultLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckDefaultLanguages", dictionaryMappings, languageToolMappings) ?? settings.DefaultLanguages,
        IdentifierLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckIdentifierLanguages", dictionaryMappings, languageToolMappings) ?? settings.IdentifierLanguages,
        ClassNameLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckClassNameLanguages", dictionaryMappings, languageToolMappings) ?? settings.ClassNameLanguages,
        MethodNameLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckMethodNameLanguages", dictionaryMappings, languageToolMappings) ?? settings.MethodNameLanguages,
        VariableNameLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckVariableNameLanguages", dictionaryMappings, languageToolMappings) ?? settings.VariableNameLanguages,
        PropertyNameLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckPropertyNameLanguages", dictionaryMappings, languageToolMappings) ?? settings.PropertyNameLanguages,
        EnumNameLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckEnumNameLanguages", dictionaryMappings, languageToolMappings) ?? settings.EnumNameLanguages,
        EnumMemberNameLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckEnumMemberNameLanguages", dictionaryMappings, languageToolMappings) ?? settings.EnumMemberNameLanguages,
        EventNameLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckEventNameLanguages", dictionaryMappings, languageToolMappings) ?? settings.EventNameLanguages,
        CommentLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckCommentLanguages", dictionaryMappings, languageToolMappings) ?? settings.CommentLanguages,
        StringLiteralLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckStringLiteralLanguages", dictionaryMappings, languageToolMappings) ?? settings.StringLiteralLanguages,
        Attributes = settings.Attributes,
        CustomDictionariesFolder = settings.CustomDictionariesFolder,
        LanguageToolUrl = ReadString(globalOptions, "YouShouldSpellcheckLanguageToolUrl") ?? settings.LanguageToolUrl,
        LanguageToolMode = settings.LanguageToolMode,
        LanguageToolScope = ReadEnum<LanguageToolScope>(globalOptions, "YouShouldSpellcheckLanguageToolScope") ?? settings.LanguageToolScope,
        LanguageToolTimeoutSeconds = ReadInteger(globalOptions, "YouShouldSpellcheckLanguageToolTimeoutSeconds") ?? settings.LanguageToolTimeoutSeconds,
        LanguageToolMaxConcurrency = ReadInteger(globalOptions, "YouShouldSpellcheckLanguageToolMaxConcurrency") ?? settings.LanguageToolMaxConcurrency,
        MaxSuggestionsPerLanguage = settings.MaxSuggestionsPerLanguage,
        MaxSuggestions = settings.MaxSuggestions,
      };

      return new SpellcheckSettingsWrapper(overriddenSettings, settingsPath);
    }

    private static string? ReadString(AnalyzerConfigOptions globalOptions, string propertyName) =>
      globalOptions.TryGetValue($"build_property.{propertyName}", out var value) ? value : null;

    private static TEnum? ReadEnum<TEnum>(AnalyzerConfigOptions globalOptions, string propertyName)
      where TEnum : struct
    {
      return globalOptions.TryGetValue($"build_property.{propertyName}", out var value)
        && Enum.TryParse(value, ignoreCase: true, out TEnum result)
          ? result
          : null;
    }

    private static int? ReadInteger(AnalyzerConfigOptions globalOptions, string propertyName) =>
      globalOptions.TryGetValue($"build_property.{propertyName}", out var value)
        && int.TryParse(value, out var result)
          ? result
          : null;

    private static Dictionary<string, string> ReadMappings(AnalyzerConfigOptions globalOptions, string propertyName)
    {
      var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      if (!TryReadListProperty(globalOptions, propertyName, out var value, out var separator)
        || string.IsNullOrWhiteSpace(value))
      {
        return mappings;
      }

      foreach (var entry in value.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries))
      {
        var separatorIndex = entry.IndexOf('=');
        if (separatorIndex <= 0 || separatorIndex == entry.Length - 1)
        {
          continue;
        }

        mappings[entry.Substring(0, separatorIndex).Trim()] = entry.Substring(separatorIndex + 1).Trim();
      }

      return mappings;
    }

    private static Language[]? ReadLanguages(
      AnalyzerConfigOptions globalOptions,
      string propertyName,
      IReadOnlyDictionary<string, string> dictionaryMappings,
      IReadOnlyDictionary<string, string> languageToolMappings)
    {
      if (!TryReadListProperty(globalOptions, propertyName, out var value, out var separator)
        || string.IsNullOrWhiteSpace(value))
      {
        return null;
      }

      return value
        .Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries)
        .Select(languageTag => languageTag.Trim())
        .Where(languageTag => languageTag.Length > 0)
        .Select(languageTag => new Language
        {
          LocalDictionaryLanguage = dictionaryMappings.TryGetValue(languageTag, out var dictionaryLanguage)
            ? dictionaryLanguage
            : languageTag,
          LanguageToolLanguage = languageToolMappings.TryGetValue(languageTag, out var languageToolLanguage)
            ? languageToolLanguage
            : languageTag,
        })
        .ToArray();
    }

    private static bool TryReadListProperty(
      AnalyzerConfigOptions globalOptions,
      string propertyName,
      out string value,
      out char separator)
    {
      var hasEncodedValue = globalOptions.TryGetValue($"build_property.{propertyName}Encoded", out var encodedValue);
      if (hasEncodedValue && !string.IsNullOrWhiteSpace(encodedValue))
      {
        value = encodedValue!;
        separator = '|';
        return true;
      }

      separator = ';';
      if (globalOptions.TryGetValue($"build_property.{propertyName}", out var legacyValue))
      {
        value = legacyValue ?? string.Empty;
        return true;
      }

      value = encodedValue ?? string.Empty;
      separator = '|';
      return hasEncodedValue;
    }

    private static ImmutableDictionary<string, DictionarySources> ReadDictionarySources(
      ImmutableArray<AdditionalText> additionalFiles,
      CancellationToken cancellationToken)
    {
      var filesByPath = additionalFiles
        .GroupBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
        .ToImmutableDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
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
    public LanguageToolCandidate(string text, Location location, ImmutableArray<string> languages, ImmutableArray<int> sourcePositions = default)
    {
      this.Text = text;
      this.Location = location;
      this.Languages = languages;
      this.SourcePositions = sourcePositions;
    }

    public string Text { get; }

    public Location Location { get; }

    public ImmutableArray<string> Languages { get; }

    public ImmutableArray<int> SourcePositions { get; }
  }

  internal enum LanguageToolTextKind
  {
    StringLiteral,
    AttributeArgument,
  }

  internal sealed class LanguageToolCandidateComparer : IEqualityComparer<LanguageToolCandidate>
  {
    public static LanguageToolCandidateComparer Instance { get; } = new();

    public bool Equals(LanguageToolCandidate? x, LanguageToolCandidate? y) =>
      ReferenceEquals(x, y)
      || (x != null
        && y != null
        && string.Equals(x.Text, y.Text, StringComparison.Ordinal)
        && Equals(x.Location.SourceTree, y.Location.SourceTree)
        && x.Location.SourceSpan.Equals(y.Location.SourceSpan)
        && x.Languages.SequenceEqual(y.Languages, StringComparer.OrdinalIgnoreCase));

    public int GetHashCode(LanguageToolCandidate candidate)
    {
      var hashCode = candidate.Text.GetHashCode();
      hashCode = (hashCode * 397) ^ candidate.Location.SourceSpan.GetHashCode();
      hashCode = (hashCode * 397) ^ (candidate.Location.SourceTree?.GetHashCode() ?? 0);
      foreach (var language in candidate.Languages)
      {
        hashCode = (hashCode * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(language);
      }

      return hashCode;
    }
  }
}
