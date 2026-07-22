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
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.Diagnostics;
  using Microsoft.CodeAnalysis.Text;
  using WeCantSpell.Hunspell;

  /// <summary>
  /// Holds all mutable caches and immutable inputs for one analyzer compilation.
  /// </summary>
  internal sealed class CompilationSpellcheckState
  {
    private const string CustomDictionaryPrefix = "CustomDictionary.";

    private readonly ImmutableDictionary<string, DictionarySources> dictionarySources;
    private readonly ImmutableDictionary<string, ImmutableHashSet<string>> customWords;
    private readonly ConcurrentDictionary<string, Lazy<WordList?>> dictionaries = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Tuple<string, string>, bool> spellingCache = new();
    private readonly ConcurrentDictionary<Tuple<string, string>, Lazy<ImmutableArray<string>>> suggestionCache = new();
    private readonly ConcurrentQueue<LanguageToolCandidate> languageToolCandidates = new();
    private readonly ConcurrentQueue<Diagnostic> languageToolFallbackDiagnostics = new();

    private CompilationSpellcheckState(
      ISpellcheckSettings settings,
      LanguageToolExecutionMode languageToolMode,
      ImmutableDictionary<string, DictionarySources> dictionarySources,
      ImmutableDictionary<string, ImmutableHashSet<string>> customWords,
      ImmutableArray<string> configurationErrors)
    {
      this.Settings = settings;
      this.LanguageToolMode = languageToolMode;
      this.dictionarySources = dictionarySources;
      this.customWords = customWords;
      this.ConfigurationErrors = configurationErrors;
    }

    public ISpellcheckSettings Settings { get; }

    public LanguageToolExecutionMode LanguageToolMode { get; }

    public ImmutableArray<string> ConfigurationErrors { get; }

    public bool LanguageToolEnabled =>
      this.LanguageToolMode != LanguageToolExecutionMode.Off
      && !string.IsNullOrWhiteSpace(this.Settings.LanguageToolUrl);

    public bool LanguageToolAutoFallback => this.LanguageToolMode == LanguageToolExecutionMode.AutoFallback;

    public static CompilationSpellcheckState Create(AnalyzerOptions options, CancellationToken cancellationToken)
    {
      var additionalFiles = options?.AdditionalFiles ?? ImmutableArray<AdditionalText>.Empty;
      var settings = ReadMsBuildSettings(options, out var configurationErrors);
      var configuredDictionaryLanguages = GetConfiguredDictionaryLanguages(settings);
      var sources = ReadDictionarySources(additionalFiles, configuredDictionaryLanguages, cancellationToken);
      var customWords = ReadCustomWords(additionalFiles, configuredDictionaryLanguages, cancellationToken);
      return new CompilationSpellcheckState(settings, settings.LanguageToolMode, sources, customWords, configurationErrors);
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

    public bool IsWordCorrect(string word, string language, CancellationToken cancellationToken)
    {
      if (string.IsNullOrEmpty(language))
      {
        return true;
      }

      var key = Tuple.Create(language, word);
      return this.spellingCache.GetOrAdd(key, _ => this.CheckWordCore(word, language, cancellationToken));
    }

    public IEnumerable<string> Suggest(string word, string language, CancellationToken cancellationToken)
    {
      if (!this.dictionarySources.ContainsKey(language))
      {
        return Enumerable.Empty<string>();
      }

      var key = Tuple.Create(language, word);
      var suggestions = this.suggestionCache.GetOrAdd(
        key,
        _ => new Lazy<ImmutableArray<string>>(
          () => this.SuggestCore(word, language, cancellationToken),
          LazyThreadSafetyMode.ExecutionAndPublication));
      try
      {
        return suggestions.Value;
      }
      catch (OperationCanceledException)
      {
        this.suggestionCache.TryRemove(key, out _);
        throw;
      }
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

    public IAttributeProperty? FindAttributeArgumentRule(
      INamedTypeSymbol attributeType,
      string memberName,
      AttributeArgumentKind kind) =>
      this.Settings.Attributes.FirstOrDefault(rule =>
        MatchesAttributeName(attributeType, rule.AttributeName)
        && string.Equals(rule.PropertyName, memberName, StringComparison.Ordinal)
        && (rule.Kind == AttributeArgumentKind.Any || rule.Kind == kind));

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

    private static ISpellcheckSettings ReadMsBuildSettings(
      AnalyzerOptions? options,
      out ImmutableArray<string> configurationErrors)
    {
      var defaults = new SpellcheckSettings();
      var globalOptions = options?.AnalyzerConfigOptionsProvider.GlobalOptions;
      if (globalOptions == null)
      {
        configurationErrors = ImmutableArray<string>.Empty;
        return new SpellcheckSettingsWrapper(defaults);
      }

      var dictionaryMappings = ReadMappings(globalOptions, "YouShouldSpellcheckDictionaryMappings");
      var languageToolMappings = ReadMappings(globalOptions, "YouShouldSpellcheckLanguageToolMappings");
      var errors = ImmutableArray.CreateBuilder<string>();
      var attributeArguments = ReadAttributeArguments(
        globalOptions,
        dictionaryMappings,
        languageToolMappings,
        out var attributeArgumentErrors);
      errors.AddRange(attributeArgumentErrors);
      var overriddenSettings = new SpellcheckSettings
      {
        DefaultLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckDefaultLanguages", dictionaryMappings, languageToolMappings, errors) ?? defaults.DefaultLanguages,
        IdentifierLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckIdentifierLanguages", dictionaryMappings, languageToolMappings, errors),
        ClassNameLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckClassNameLanguages", dictionaryMappings, languageToolMappings, errors),
        MethodNameLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckMethodNameLanguages", dictionaryMappings, languageToolMappings, errors),
        VariableNameLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckVariableNameLanguages", dictionaryMappings, languageToolMappings, errors),
        PropertyNameLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckPropertyNameLanguages", dictionaryMappings, languageToolMappings, errors),
        EnumNameLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckEnumNameLanguages", dictionaryMappings, languageToolMappings, errors),
        EnumMemberNameLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckEnumMemberNameLanguages", dictionaryMappings, languageToolMappings, errors),
        EventNameLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckEventNameLanguages", dictionaryMappings, languageToolMappings, errors),
        CommentLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckCommentLanguages", dictionaryMappings, languageToolMappings, errors),
        StringLiteralLanguages = ReadLanguages(globalOptions, "YouShouldSpellcheckStringLiteralLanguages", dictionaryMappings, languageToolMappings, errors),
        Attributes = attributeArguments,
        LanguageToolUrl = ReadString(globalOptions, "YouShouldSpellcheckLanguageToolUrl") ?? defaults.LanguageToolUrl,
        LanguageToolMode = ReadEnum<LanguageToolExecutionMode>(globalOptions, "YouShouldSpellcheckLanguageToolMode") ?? defaults.LanguageToolMode,
        LanguageToolScope = ReadEnum<LanguageToolScope>(globalOptions, "YouShouldSpellcheckLanguageToolScope") ?? defaults.LanguageToolScope,
        LanguageToolTimeoutSeconds = ReadInteger(globalOptions, "YouShouldSpellcheckLanguageToolTimeoutSeconds") ?? defaults.LanguageToolTimeoutSeconds,
        LanguageToolMaxConcurrency = ReadInteger(globalOptions, "YouShouldSpellcheckLanguageToolMaxConcurrency") ?? defaults.LanguageToolMaxConcurrency,
        MaxSuggestionsPerLanguage = ReadInteger(globalOptions, "YouShouldSpellcheckMaxSuggestionsPerLanguage") ?? defaults.MaxSuggestionsPerLanguage,
        MaxSuggestions = ReadInteger(globalOptions, "YouShouldSpellcheckMaxSuggestions") ?? defaults.MaxSuggestions,
      };

      configurationErrors = errors.ToImmutable();
      return new SpellcheckSettingsWrapper(overriddenSettings);
    }

    private static AttributeProperty[]? ReadAttributeArguments(
      AnalyzerConfigOptions globalOptions,
      IReadOnlyDictionary<string, string> dictionaryMappings,
      IReadOnlyDictionary<string, string> languageToolMappings,
      out ImmutableArray<string> configurationErrors)
    {
      const string propertyName = "build_property.YouShouldSpellcheckAttributeArgumentsEncoded";
      if (!globalOptions.TryGetValue(propertyName, out var encodedValue)
        || string.IsNullOrWhiteSpace(encodedValue))
      {
        configurationErrors = ImmutableArray<string>.Empty;
        return null;
      }

      var rules = new List<AttributeProperty>();
      var errors = ImmutableArray.CreateBuilder<string>();
      foreach (var record in encodedValue.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
      {
        var fields = record.Split(new[] { '~' }, StringSplitOptions.None);
        if (fields.Length != 4)
        {
          errors.Add($"Invalid YouShouldSpellcheckAttributeArgument record '{record}'. Expected AttributeType, Member, Kind, and Languages metadata.");
          continue;
        }

        var attributeName = fields[0].Trim();
        var memberName = fields[1].Trim();
        var kindText = fields[2].Trim();
        var languagesText = fields[3].Trim();
        if (attributeName.Length == 0 || memberName.Length == 0 || languagesText.Length == 0)
        {
          errors.Add($"Invalid YouShouldSpellcheckAttributeArgument record '{record}'. AttributeType, Member, and Languages must be non-empty.");
          continue;
        }

        var kind = AttributeArgumentKind.Any;
        if (kindText.Length > 0
          && !Enum.TryParse(kindText, ignoreCase: true, out kind))
        {
          errors.Add($"Invalid attribute argument Kind '{kindText}' for '{attributeName}.{memberName}'. Use Any, NamedMember, or ConstructorParameter.");
          continue;
        }

        var languages = CreateLanguages(
          languagesText,
          ',',
          dictionaryMappings,
          languageToolMappings);
        if (languages.Length == 0)
        {
          errors.Add($"Attribute argument rule '{attributeName}.{memberName}' must specify at least one language.");
          continue;
        }

        rules.Add(new AttributeProperty
        {
          AttributeName = attributeName,
          PropertyName = memberName,
          Kind = kind,
          Languages = languages,
        });
      }

      configurationErrors = errors.ToImmutable();
      return rules.ToArray();
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
      IReadOnlyDictionary<string, string> languageToolMappings,
      ImmutableArray<string>.Builder configurationErrors)
    {
      if (!TryReadListProperty(globalOptions, propertyName, out var value, out var separator)
        || string.IsNullOrWhiteSpace(value))
      {
        return null;
      }

      var languageTags = value
        .Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries)
        .Select(languageTag => languageTag.Trim())
        .Where(languageTag => languageTag.Length > 0)
        .ToArray();
      var hasNone = languageTags.Any(languageTag => string.Equals(languageTag, "none", StringComparison.OrdinalIgnoreCase));
      if (hasNone && languageTags.Length == 1)
      {
        return [];
      }

      if (hasNone)
      {
        configurationErrors.Add($"Invalid {propertyName} value. The 'none' sentinel must be the only configured language.");
        languageTags = languageTags
          .Where(languageTag => !string.Equals(languageTag, "none", StringComparison.OrdinalIgnoreCase))
          .ToArray();
      }

      return CreateLanguages(languageTags, dictionaryMappings, languageToolMappings);
    }

    private static Language[] CreateLanguages(
      string value,
      char separator,
      IReadOnlyDictionary<string, string> dictionaryMappings,
      IReadOnlyDictionary<string, string> languageToolMappings) =>
      CreateLanguages(
        value.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries),
        dictionaryMappings,
        languageToolMappings);

    private static Language[] CreateLanguages(
      IEnumerable<string> languageTags,
      IReadOnlyDictionary<string, string> dictionaryMappings,
      IReadOnlyDictionary<string, string> languageToolMappings) =>
      languageTags
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

    private static bool MatchesAttributeName(INamedTypeSymbol attributeType, string configuredName)
    {
      var normalizedConfiguredName = configuredName.Trim();
      if (normalizedConfiguredName.StartsWith("global::", StringComparison.Ordinal))
      {
        normalizedConfiguredName = normalizedConfiguredName.Substring("global::".Length);
      }

      var fullName = attributeType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
      return string.Equals(normalizedConfiguredName, fullName, StringComparison.Ordinal)
        || string.Equals(normalizedConfiguredName, RemoveAttributeSuffix(fullName), StringComparison.Ordinal)
        || string.Equals(normalizedConfiguredName, attributeType.Name, StringComparison.Ordinal)
        || string.Equals(normalizedConfiguredName, RemoveAttributeSuffix(attributeType.Name), StringComparison.Ordinal);
    }

    private static string RemoveAttributeSuffix(string attributeName)
    {
      const string suffix = "Attribute";
      return attributeName.EndsWith(suffix, StringComparison.Ordinal)
        ? attributeName.Substring(0, attributeName.Length - suffix.Length)
        : attributeName;
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

    private static ImmutableHashSet<string> GetConfiguredDictionaryLanguages(ISpellcheckSettings settings)
    {
      var languages = new[]
      {
        settings.DefaultLanguages,
        settings.IdentifierLanguages,
        settings.ClassNameLanguages,
        settings.MethodNameLanguages,
        settings.VariableNameLanguages,
        settings.PropertyNameLanguages,
        settings.EnumNameLanguages,
        settings.EnumMemberNameLanguages,
        settings.EventNameLanguages,
        settings.CommentLanguages,
        settings.StringLiteralLanguages,
      }
        .SelectMany(configuredLanguages => configuredLanguages)
        .Concat(settings.Attributes.SelectMany(attribute => attribute.Languages))
        .Select(language => language.LocalDictionaryLanguage)
        .Where(language => !string.IsNullOrWhiteSpace(language));

      return languages.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static ImmutableDictionary<string, DictionarySources> ReadDictionarySources(
      ImmutableArray<AdditionalText> additionalFiles,
      ImmutableHashSet<string> configuredDictionaryLanguages,
      CancellationToken cancellationToken)
    {
      var filesByPath = additionalFiles
        .GroupBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
        .ToImmutableDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
      var builder = ImmutableDictionary.CreateBuilder<string, DictionarySources>(StringComparer.OrdinalIgnoreCase);
      foreach (var dictionaryFile in additionalFiles.Where(file => string.Equals(Path.GetExtension(file.Path), ".dic", StringComparison.OrdinalIgnoreCase)))
      {
        cancellationToken.ThrowIfCancellationRequested();
        var dictionaryLanguage = Path.GetFileNameWithoutExtension(dictionaryFile.Path);
        if (!configuredDictionaryLanguages.Contains(dictionaryLanguage))
        {
          continue;
        }

        var affixPath = Path.ChangeExtension(dictionaryFile.Path, ".aff");
        if (!filesByPath.TryGetValue(affixPath, out var affixFile))
        {
          continue;
        }

        var dictionaryText = dictionaryFile.GetText(cancellationToken);
        var affixText = affixFile.GetText(cancellationToken);
        if (dictionaryText != null && affixText != null)
        {
          builder[dictionaryLanguage] = new DictionarySources(dictionaryText, affixText);
        }
      }

      return builder.ToImmutable();
    }

    private static ImmutableDictionary<string, ImmutableHashSet<string>> ReadCustomWords(
      ImmutableArray<AdditionalText> additionalFiles,
      ImmutableHashSet<string> configuredDictionaryLanguages,
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
        if (!configuredDictionaryLanguages.Contains(language))
        {
          continue;
        }

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

    private bool CheckWordCore(string word, string language, CancellationToken cancellationToken)
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
      return dictionary.Value?.Check(word, cancellationToken) ?? true;
    }

    private ImmutableArray<string> SuggestCore(string word, string language, CancellationToken cancellationToken)
    {
      var dictionary = this.dictionaries.GetOrAdd(
        language,
        key => new Lazy<WordList?>(() => this.CreateDictionary(key), LazyThreadSafetyMode.ExecutionAndPublication));
      return dictionary.Value?.Suggest(word, cancellationToken).ToImmutableArray() ?? ImmutableArray<string>.Empty;
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
      // Hunspell interprets both streams according to the SET directive in the affix file.
      // Preserve Roslyn's detected AdditionalText encoding so that directive remains accurate.
      var encoding = sourceText.Encoding ?? new UTF8Encoding(false);
      using (var writer = new StreamWriter(stream, encoding, 1024, leaveOpen: true))
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
