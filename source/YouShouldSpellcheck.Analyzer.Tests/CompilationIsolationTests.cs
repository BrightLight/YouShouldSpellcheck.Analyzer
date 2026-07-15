namespace YouShouldSpellcheck.Analyzer.Test
{
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
    private const string Settings = """
      <SpellcheckSettings>
        <DefaultLanguages>
          <Language LocalDictionaryLanguage="en_US" LanguageToolLanguage="en-US" />
        </DefaultLanguages>
      </SpellcheckSettings>
      """;

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

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
      DiagnosticAnalyzer analyzer,
      string assemblyName,
      string customWords,
      string source = "public class Zorbax { }")
    {
      var syntaxTree = CSharpSyntaxTree.ParseText(source);
      var compilation = CSharpCompilation.Create(
        assemblyName,
        new[] { syntaxTree },
        new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

      var dictionaryFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "dic");
      var additionalFiles = ImmutableArray.Create<AdditionalText>(
        new InMemoryAdditionalText("/config/youshouldspellcheck.config.xml", Settings),
        new InMemoryAdditionalText("/dic/en_US.dic", File.ReadAllText(Path.Combine(dictionaryFolder, "en_US.dic"))),
        new InMemoryAdditionalText("/dic/en_US.aff", File.ReadAllText(Path.Combine(dictionaryFolder, "en_US.aff"))),
        new InMemoryAdditionalText("/custom/CustomDictionary.en_US.txt", customWords));
      var options = new AnalyzerOptions(additionalFiles);

      return await compilation
        .WithAnalyzers(ImmutableArray.Create(analyzer), options)
        .GetAnalyzerDiagnosticsAsync(CancellationToken.None);
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
