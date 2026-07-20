namespace YouShouldSpellcheck.Analyzer.Test
{
  using System;
  using System.Collections.Generic;
  using System.Collections.Immutable;
  using System.Composition.Hosting;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CodeActions;
  using Microsoft.CodeAnalysis.CodeFixes;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.Text;
  using NUnit.Framework;
  using YouShouldSpellcheck.Analyzer.CodeFixes;

  [TestFixture]
  public class LanguageToolCodeFixTests
  {
    private const string Source = "[Display(Name = @\"Nach den einen folgte eine andere Tag.\")] public class Test { }";

    [Test]
    public void TextCodeFixProviderIsDiscoverableThroughMef()
    {
      using var container = new ContainerConfiguration()
        .WithAssembly(typeof(TextCodeFixProvider).Assembly)
        .CreateContainer();

      Assert.That(container.GetExports<CodeFixProvider>(),
        Has.Some.TypeOf<TextCodeFixProvider>());
    }

    [Test]
    public async Task RegistersAndAppliesMultiWordGrammarSuggestions()
    {
      using var workspace = new AdhocWorkspace();
      var project = workspace.AddProject("CodeFixTest", LanguageNames.CSharp)
        .WithParseOptions(CSharpParseOptions.Default);
      var document = workspace.AddDocument(
        project.Id,
        "Test.cs",
        SourceText.From(Source));
      var syntaxTree = await document.GetSyntaxTreeAsync();
      var offendingText = "eine andere Tag";
      var properties = ImmutableDictionary<string, string>.Empty
        .Add("offendingWord", offendingText)
        .Add("CategoryId", "GRAMMAR")
        .Add("LanguageToolRuleId", "DE_AGREEMENT")
        .Add("LanguageToolRuleIssueType", "uncategorized")
        .Add("suggestion_1", "ein anderer Tag")
        .Add("suggestion_2", "einen anderen Tag")
        .Add("suggestion_3", "einem anderen Tag")
        .Add("suggestion_4", "ein anderes Tag");
      var descriptor = new DiagnosticDescriptor(
        SpellcheckAnalyzerBase.LanguageToolGrammarDiagnosticId,
        "LanguageTool: Grammar",
        "{0}",
        "LanguageTool",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);
      var diagnostic = Diagnostic.Create(
        descriptor,
        Location.Create(
          syntaxTree!,
          new TextSpan(Source.IndexOf(offendingText, StringComparison.Ordinal), offendingText.Length)),
        properties,
        "Agreement error");
      var actions = new List<CodeAction>();
      var context = new CodeFixContext(
        document,
        diagnostic,
        (action, _) => actions.Add(action),
        CancellationToken.None);

      await new TextCodeFixProvider().RegisterCodeFixesAsync(context);

      Assert.That(actions.Select(action => action.Title), Is.EquivalentTo(new[]
      {
        "Replace with (LanguageTool): ein anderer Tag",
        "Replace with (LanguageTool): einen anderen Tag",
        "Replace with (LanguageTool): einem anderen Tag",
        "Replace with (LanguageTool): ein anderes Tag",
      }));

      var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
      var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
      var changedText = await changedSolution.GetDocument(document.Id)!.GetTextAsync();
      Assert.That(changedText.ToString(), Does.Contain("Nach den einen folgte ein anderer Tag."));
    }

    [Test]
    public async Task RegistersGrammarSuggestionsWhenBuildHostDropsDiagnosticProperties()
    {
      using var workspace = new AdhocWorkspace();
      var project = workspace.AddProject("CodeFixTest", LanguageNames.CSharp)
        .WithParseOptions(CSharpParseOptions.Default);
      var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(Source));
      var syntaxTree = await document.GetSyntaxTreeAsync();
      var offendingText = "eine andere Tag";
      var descriptor = new DiagnosticDescriptor(
        SpellcheckAnalyzerBase.LanguageToolGrammarDiagnosticId,
        "LanguageTool: Grammar",
        "{0}",
        "LanguageTool",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);
      var diagnostic = Diagnostic.Create(
        descriptor,
        Location.Create(
          syntaxTree!,
          new TextSpan(Source.IndexOf(offendingText, StringComparison.Ordinal), offendingText.Length)),
        "Agreement error\r\nReplace with\r\nein anderer Tag\r\neinen anderen Tag\r\n");
      var actions = new List<CodeAction>();
      var context = new CodeFixContext(
        document,
        diagnostic,
        (action, _) => actions.Add(action),
        CancellationToken.None);

      await new TextCodeFixProvider().RegisterCodeFixesAsync(context);

      Assert.That(actions.Select(action => action.Title), Is.EquivalentTo(new[]
      {
        "Replace with (LanguageTool): ein anderer Tag",
        "Replace with (LanguageTool): einen anderen Tag",
      }));
    }
  }
}
