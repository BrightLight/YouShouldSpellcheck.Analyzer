namespace YouShouldSpellcheck.Analyzer.Test
{
  using System.Collections.Generic;
  using System.Collections.Immutable;
  using System.IO;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.Diagnostics;
  using Microsoft.CodeAnalysis.Text;
  using NUnit.Framework;

  [TestFixture]
  public class CompilationIsolationTests
  {
    [Test]
    public async Task ConcurrentCompilationsKeepAdditionalFilesIsolated()
    {
      const string projectSpecificWord = "Zorbax";
      var analyzer = new ClassNameSpellcheckAnalyzer();
      var firstAnalysis = AnalyzeAsync(analyzer, "FirstProject", projectSpecificWord);
      var secondAnalysis = AnalyzeAsync(analyzer, "SecondProject", string.Empty);

      var results = await Task.WhenAll(firstAnalysis, secondAnalysis);

      Assert.That(results[0], Is.Empty, "The first compilation should accept its custom word.");
      Assert.That(results[1].Select(diagnostic => diagnostic.Id), Is.EquivalentTo(new[] { ClassNameSpellcheckAnalyzer.ClassNameDiagnosticId }));
    }

    [Test]
    public async Task DiagnosticCarriesHunspellSuggestionsForSeparateCodeFixHost()
    {
      var diagnostics = await AnalyzeAsync(
        new StringLiteralSpellcheckAnalyzer(),
        "SuggestionProject",
        string.Empty,
        "public class TypeName { public const string Text = \"smple\"; }");

      var diagnostic = diagnostics.Single(result => result.Id == StringLiteralSpellcheckAnalyzer.StringLiteralDiagnosticId);
      var suggestions = diagnostic.Properties
        .Where(property => property.Key.StartsWith("localSuggestion_", System.StringComparison.Ordinal)
          && !property.Key.StartsWith("localSuggestionLanguage_", System.StringComparison.Ordinal))
        .Select(property => property.Value)
        .ToArray();
      var suggestionLanguages = diagnostic.Properties
        .Where(property => property.Key.StartsWith("localSuggestionLanguage_", System.StringComparison.Ordinal))
        .Select(property => property.Value)
        .ToArray();

      Assert.That(diagnostic.Properties["offendingWord"], Is.EqualTo("smple"));
      Assert.That(suggestions, Does.Contain("simple"));
      Assert.That(suggestionLanguages, Is.Not.Empty.And.All.EqualTo("en_US"));
    }

    [Test]
    public async Task SuggestionLimitsKeepTheHighestRankedHunspellSuggestions()
    {
      const string source = "public class TypeName { public const string Text = \"lne\"; }";
      var defaultSuggestions = await GetSuggestionsAsync(source);
      var limitedSuggestions = await GetSuggestionsAsync(source, maxSuggestionsPerLanguage: 2, maxSuggestions: 2);

      Assert.That(defaultSuggestions, Has.Length.EqualTo(5));
      Assert.That(defaultSuggestions, Does.Contain("line"));
      Assert.That(limitedSuggestions, Is.EqualTo(defaultSuggestions.Take(2)));
    }

    private static async Task<string[]> GetSuggestionsAsync(
      string source,
      int? maxSuggestionsPerLanguage = null,
      int? maxSuggestions = null)
    {
      var diagnostics = await AnalyzeAsync(
        new StringLiteralSpellcheckAnalyzer(),
        "SuggestionLimitProject",
        string.Empty,
        source,
        maxSuggestionsPerLanguage,
        maxSuggestions);

      return diagnostics.Single(result => result.Id == StringLiteralSpellcheckAnalyzer.StringLiteralDiagnosticId)
        .Properties
        .Where(property => property.Key.StartsWith("localSuggestion_", System.StringComparison.Ordinal)
          && !property.Key.StartsWith("localSuggestionLanguage_", System.StringComparison.Ordinal))
        .OrderBy(property => property.Key, System.StringComparer.Ordinal)
        .Select(property => property.Value)
        .ToArray();
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
      DiagnosticAnalyzer analyzer,
      string assemblyName,
      string customWords,
      string source = "public class Zorbax { }",
      int? maxSuggestionsPerLanguage = null,
      int? maxSuggestions = null)
    {
      var syntaxTree = CSharpSyntaxTree.ParseText(source);
      var compilation = CSharpCompilation.Create(
        assemblyName,
        new[] { syntaxTree },
        new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

      var dictionaryFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "dictionaries");
      var additionalFiles = ImmutableArray.Create<AdditionalText>(
        new InMemoryAdditionalText("/dictionaries/en_US.dic", File.ReadAllText(Path.Combine(dictionaryFolder, "en_US.dic"))),
        new InMemoryAdditionalText("/dictionaries/en_US.aff", File.ReadAllText(Path.Combine(dictionaryFolder, "en_US.aff"))),
        new InMemoryAdditionalText("/custom/CustomDictionary.en_US.txt", customWords));
      var globalOptions = ImmutableDictionary<string, string>.Empty
        .Add("build_property.YouShouldSpellcheckDefaultLanguagesEncoded", "en-US")
        .Add("build_property.YouShouldSpellcheckDictionaryMappingsEncoded", "en-US=en_US");
      if (maxSuggestionsPerLanguage != null)
      {
        globalOptions = globalOptions.Add(
          "build_property.YouShouldSpellcheckMaxSuggestionsPerLanguage",
          maxSuggestionsPerLanguage.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
      }

      if (maxSuggestions != null)
      {
        globalOptions = globalOptions.Add(
          "build_property.YouShouldSpellcheckMaxSuggestions",
          maxSuggestions.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
      }

      var options = new AnalyzerOptions(additionalFiles, new TestAnalyzerConfigOptionsProvider(globalOptions));

      return await compilation
        .WithAnalyzers(ImmutableArray.Create(analyzer), options)
        .GetAnalyzerDiagnosticsAsync(CancellationToken.None);
    }

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
      private static readonly AnalyzerConfigOptions EmptyOptions = new TestAnalyzerConfigOptions(ImmutableDictionary<string, string>.Empty);

      public TestAnalyzerConfigOptionsProvider(ImmutableDictionary<string, string> globalOptions)
      {
        this.GlobalOptions = new TestAnalyzerConfigOptions(globalOptions);
      }

      public override AnalyzerConfigOptions GlobalOptions { get; }

      public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => EmptyOptions;

      public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => EmptyOptions;
    }

    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
      private readonly IReadOnlyDictionary<string, string> options;

      public TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> options)
      {
        this.options = options;
      }

      public override bool TryGetValue(string key, out string value) => this.options.TryGetValue(key, out value);
    }

    private sealed class InMemoryAdditionalText : AdditionalText
    {
      private readonly SourceText text;

      public InMemoryAdditionalText(string path, string text)
      {
        this.Path = path;
        this.text = SourceText.From(text);
      }

      public override string Path { get; }

      public override SourceText GetText(CancellationToken cancellationToken = default)
      {
        cancellationToken.ThrowIfCancellationRequested();
        return this.text;
      }
    }
  }
}
