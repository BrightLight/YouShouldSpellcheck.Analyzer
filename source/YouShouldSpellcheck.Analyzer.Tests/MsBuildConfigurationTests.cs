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
    private const string AttributeSource = """
      using UiText = Attributes.UiTextAttribute;

      [UiText(1, "escapng", Caption = "Temprature")]
      public class TypeName
      {
      }

      namespace Attributes
      {
        public sealed class UiTextAttribute : System.Attribute
        {
          public UiTextAttribute(string label, int count)
          {
          }

          public UiTextAttribute(int count, string text)
          {
          }

          public string Caption { get; set; } = string.Empty;
        }
      }
      """;

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

    [Test]
    public async Task NoneLanguageSentinelDisablesCategoryFallback()
    {
      var globalOptions = ImmutableDictionary<string, string>.Empty
        .Add("build_property.YouShouldSpellcheckDefaultLanguagesEncoded", "en-US")
        .Add("build_property.YouShouldSpellcheckStringLiteralLanguagesEncoded", "NoNe")
        .Add("build_property.YouShouldSpellcheckDictionaryMappingsEncoded", "en-US=en_US");

      var diagnostics = await AnalyzeStringLiteralsAsync(
        "public class TypeName { public const string Text = \"Temprature\"; }",
        globalOptions);

      Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public async Task NoneLanguageSentinelCannotBeCombinedWithLanguages()
    {
      var globalOptions = ImmutableDictionary<string, string>.Empty
        .Add("build_property.YouShouldSpellcheckDefaultLanguagesEncoded", "en-US")
        .Add("build_property.YouShouldSpellcheckStringLiteralLanguagesEncoded", "en-US|none")
        .Add("build_property.YouShouldSpellcheckDictionaryMappingsEncoded", "en-US=en_US");

      var diagnostics = await AnalyzeStringLiteralsAsync(
        "public class TypeName { public const string Text = \"Temprature\"; }",
        globalOptions);

      Assert.That(diagnostics, Has.One.Matches<Diagnostic>(diagnostic =>
        diagnostic.Id == StringLiteralSpellcheckAnalyzer.ConfigurationDiagnosticId
        && diagnostic.GetMessage().Contains("must be the only configured language")));
    }

    [Test]
    public async Task SuggestionLimitPropertiesOverrideDefaults()
    {
      var baseOptions = ImmutableDictionary<string, string>.Empty
        .Add("build_property.YouShouldSpellcheckDefaultLanguagesEncoded", "en-US")
        .Add("build_property.YouShouldSpellcheckDictionaryMappingsEncoded", "en-US=en_US");
      var perLanguageDiagnostics = await AnalyzeStringLiteralsAsync(
        "public class TypeName { public const string Text = \"lne\"; }",
        baseOptions
          .Add("build_property.YouShouldSpellcheckMaxSuggestionsPerLanguage", "1")
          .Add("build_property.YouShouldSpellcheckMaxSuggestions", "8"));
      var overallDiagnostics = await AnalyzeStringLiteralsAsync(
        "public class TypeName { public const string Text = \"lne\"; }",
        baseOptions
          .Add("build_property.YouShouldSpellcheckMaxSuggestionsPerLanguage", "5")
          .Add("build_property.YouShouldSpellcheckMaxSuggestions", "2"));

      Assert.That(CountSuggestions(perLanguageDiagnostics.Single()), Is.EqualTo(1));
      Assert.That(CountSuggestions(overallDiagnostics.Single()), Is.EqualTo(2));
    }

    [Test]
    public async Task AttributeArgumentItemsMatchAliasNamedMemberAndSelectedConstructor()
    {
      var diagnostics = await AnalyzeAttributeArgumentsAsync(
        AttributeSource,
        "Attributes.UiTextAttribute~text~ConstructorParameter~en-US|Attributes.UiTextAttribute~Caption~NamedMember~en-US");

      Assert.That(
        diagnostics.Select(diagnostic => (diagnostic.Id, diagnostic.GetMessage())),
        Is.EquivalentTo(new[]
        {
          (StringLiteralSpellcheckAnalyzer.AttributeArgumentStringDiagnosticId, "Possible spelling mistake: escapng"),
          (StringLiteralSpellcheckAnalyzer.AttributeArgumentStringDiagnosticId, "Possible spelling mistake: Temprature"),
        }));
    }

    [Test]
    public async Task AttributeArgumentKindDoesNotMatchAnotherArgumentKind()
    {
      var diagnostics = await AnalyzeAttributeArgumentsAsync(
        AttributeSource,
        "Attributes.UiTextAttribute~Caption~ConstructorParameter~en-US");

      Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public async Task ConcurrentAttributeArgumentItemsStayCompilationScoped()
    {
      var constructorAnalysis = AnalyzeAttributeArgumentsAsync(
        AttributeSource,
        "Attributes.UiTextAttribute~text~ConstructorParameter~en-US");
      var memberAnalysis = AnalyzeAttributeArgumentsAsync(
        AttributeSource,
        "Attributes.UiTextAttribute~Caption~NamedMember~en-US");

      var results = await Task.WhenAll(constructorAnalysis, memberAnalysis);

      Assert.That(
        results[0].Select(diagnostic => diagnostic.GetMessage()),
        Is.EqualTo(new[] { "Possible spelling mistake: escapng" }));
      Assert.That(
        results[1].Select(diagnostic => diagnostic.GetMessage()),
        Is.EqualTo(new[] { "Possible spelling mistake: Temprature" }));
    }

    [Test]
    public async Task AttributeArgumentItemsReplaceXmlAttributeRules()
    {
      const string settings = """
        <SpellcheckSettings>
          <Attributes>
            <AttributeProperty>
              <AttributeName>UiTextAttribute</AttributeName>
              <PropertyName>Caption</PropertyName>
              <Languages>
                <Language LocalDictionaryLanguage="en_US" LanguageToolLanguage="en-US" />
              </Languages>
            </AttributeProperty>
          </Attributes>
        </SpellcheckSettings>
        """;
      var diagnostics = await AnalyzeAttributeArgumentsAsync(
        AttributeSource,
        "Attributes.UiTextAttribute~text~~en-US",
        settings);

      Assert.That(
        diagnostics.Select(diagnostic => diagnostic.GetMessage()),
        Is.EqualTo(new[] { "Possible spelling mistake: escapng" }));
    }

    [Test]
    public async Task InvalidAttributeArgumentItemReportsConfigurationDiagnostic()
    {
      var diagnostics = await AnalyzeAttributeArgumentsAsync(
        "public class TypeName { }",
        "Attributes.UiTextAttribute~Caption~Unsupported~en-US");

      Assert.That(diagnostics, Has.One.Matches<Diagnostic>(diagnostic =>
        diagnostic.Id == StringLiteralSpellcheckAnalyzer.ConfigurationDiagnosticId
        && diagnostic.GetMessage().Contains("Use Any, NamedMember, or ConstructorParameter.")));
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAttributeArgumentsAsync(
      string source,
      string encodedAttributeArguments,
      string xmlSettings = null)
    {
      var globalOptions = ImmutableDictionary<string, string>.Empty
        .Add("build_property.YouShouldSpellcheckDictionaryMappingsEncoded", "en-US=en_US")
        .Add("build_property.YouShouldSpellcheckAttributeArgumentsEncoded", encodedAttributeArguments);
      return await AnalyzeStringLiteralsAsync(source, globalOptions, xmlSettings);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeStringLiteralsAsync(
      string source,
      ImmutableDictionary<string, string> globalOptions,
      string xmlSettings = null)
    {
      var compilation = CSharpCompilation.Create(
        "StringLiteralMsBuildConfigurationTest",
        new[] { CSharpSyntaxTree.ParseText(source) },
        new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
      var dictionaryFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "dictionaries");
      var additionalFiles = ImmutableArray.CreateBuilder<AdditionalText>();
      additionalFiles.Add(new InMemoryAdditionalText(
        "/dictionaries/en_US.dic",
        File.ReadAllText(Path.Combine(dictionaryFolder, "en_US.dic"))));
      additionalFiles.Add(new InMemoryAdditionalText(
        "/dictionaries/en_US.aff",
        File.ReadAllText(Path.Combine(dictionaryFolder, "en_US.aff"))));
      if (xmlSettings != null)
      {
        additionalFiles.Add(new InMemoryAdditionalText("/config/youshouldspellcheck.config.xml", xmlSettings));
      }

      var options = new AnalyzerOptions(
        additionalFiles.ToImmutable(),
        new TestAnalyzerConfigOptionsProvider(globalOptions));

      return await compilation
        .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new StringLiteralSpellcheckAnalyzer()), options)
        .GetAnalyzerDiagnosticsAsync(CancellationToken.None);
    }

    private static int CountSuggestions(Diagnostic diagnostic) =>
      diagnostic.Properties.Count(property =>
        property.Key.StartsWith("localSuggestion_", System.StringComparison.Ordinal)
        && !property.Key.StartsWith("localSuggestionLanguage_", System.StringComparison.Ordinal));

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
