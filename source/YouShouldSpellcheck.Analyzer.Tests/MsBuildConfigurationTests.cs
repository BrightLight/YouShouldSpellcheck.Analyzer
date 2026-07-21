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
  public class MsBuildConfigurationTests
  {
    [Test]
    public async Task Bcp47LanguagePropertyUsesMappedHunspellDictionary()
    {
      var compilation = CSharpCompilation.Create(
        "MsBuildConfigurationTest",
        new[] { CSharpSyntaxTree.ParseText("public class Zorbax { }") },
        new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
      var dictionaryFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "dictionaries");
      var additionalFiles = ImmutableArray.Create<AdditionalText>(
        new InMemoryAdditionalText("/dictionaries/en_US.dic", File.ReadAllText(Path.Combine(dictionaryFolder, "en_US.dic"))),
        new InMemoryAdditionalText("/dictionaries/en_US.aff", File.ReadAllText(Path.Combine(dictionaryFolder, "en_US.aff"))));
      var globalOptions = ImmutableDictionary<string, string>.Empty
        .Add("build_property.YouShouldSpellcheckDefaultLanguages", "en-US")
        .Add("build_property.YouShouldSpellcheckDictionaryMappings", "en-US=en_US");
      var options = new AnalyzerOptions(additionalFiles, new TestAnalyzerConfigOptionsProvider(globalOptions));

      var diagnostics = await compilation
        .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new YouShouldSpellcheckDiagnosticAnalyzer()), options)
        .GetAnalyzerDiagnosticsAsync(CancellationToken.None);

      Assert.That(diagnostics, Has.One.Matches<Diagnostic>(diagnostic => diagnostic.Id == ClassNameSpellcheckAnalyzer.ClassNameDiagnosticId));
    }

    [Test]
    public async Task EmptyCompilerVisibleCategoryPropertiesUseDefaultLanguages()
    {
      const string source = """
        public class MealPlanner
        {
          /// <summary>
          /// This mehtod prepares the meal.
          /// </summary>
          public string PrepateMeal()
          {
            return string.Empty;
          }
        }
        """;
      var syntaxTree = CSharpSyntaxTree.ParseText(
        source,
        new CSharpParseOptions(documentationMode: DocumentationMode.Diagnose));
      var compilation = CSharpCompilation.Create(
        "EmptyCompilerVisibleCategoryPropertiesTest",
        new[] { syntaxTree },
        new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
      var dictionaryFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "dictionaries");
      var additionalFiles = ImmutableArray.Create<AdditionalText>(
        new InMemoryAdditionalText("/dictionaries/en_US.dic", File.ReadAllText(Path.Combine(dictionaryFolder, "en_US.dic"))),
        new InMemoryAdditionalText("/dictionaries/en_US.aff", File.ReadAllText(Path.Combine(dictionaryFolder, "en_US.aff"))));
      var globalOptions = ImmutableDictionary<string, string>.Empty
        .Add("build_property.YouShouldSpellcheckDefaultLanguagesEncoded", "en-US")
        .Add("build_property.YouShouldSpellcheckDictionaryMappingsEncoded", "de-DE=de_DE_frami|en-US=en_US")
        .Add("build_property.YouShouldSpellcheckIdentifierLanguagesEncoded", string.Empty)
        .Add("build_property.YouShouldSpellcheckMethodNameLanguagesEncoded", string.Empty)
        .Add("build_property.YouShouldSpellcheckCommentLanguagesEncoded", string.Empty);
      var options = new AnalyzerOptions(additionalFiles, new TestAnalyzerConfigOptionsProvider(globalOptions));

      var diagnostics = await compilation
        .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new YouShouldSpellcheckDiagnosticAnalyzer()), options)
        .GetAnalyzerDiagnosticsAsync(CancellationToken.None);

      Assert.That(
        diagnostics.Select(diagnostic => (diagnostic.Id, diagnostic.GetMessage())),
        Is.EquivalentTo(new[]
        {
          (MethodNameSpellcheckAnalyzer.MethodNameDiagnosticId, "Possible spelling mistake: Prepate"),
          (XmlTextSpellcheckAnalyzer.CommentDiagnosticId, "Possible spelling mistake: mehtod"),
        }));
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

      public override SourceText GetText(CancellationToken cancellationToken = default) => this.text;
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
  }
}
