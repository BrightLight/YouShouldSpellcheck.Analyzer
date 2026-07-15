namespace YouShouldSpellcheck.Analyzer.Test
{
  using System;
  using System.Collections.Generic;
  using System.Collections.Immutable;
  using System.IO;
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
  public class CustomDictionaryCodeFixTests
  {
    private const string Source = "public class TypeName { public const string Text = \"smple\"; }";

    [Test]
    public async Task AddsWordToExistingCustomDictionary()
    {
      using var workspace = new AdhocWorkspace();
      var setup = await CreateCodeFixContextAsync(workspace, "First\r\n");

      var action = setup.Actions.Single(candidate =>
        candidate.Title == "Add \"smple\" to custom dictionary for en_US");
      var changedSolution = await GetChangedSolutionAsync(action);
      var customDictionary = changedSolution.GetProject(setup.ProjectId)!.AdditionalDocuments.Single();

      Assert.That((await customDictionary.GetTextAsync()).ToString(), Is.EqualTo("First\r\nsmple\r\n"));
    }

    [Test]
    public async Task CreatesMissingCustomDictionaryAndAddsWord()
    {
      using var workspace = new AdhocWorkspace();
      var setup = await CreateCodeFixContextAsync(workspace, customDictionaryText: null);

      var action = setup.Actions.Single(candidate =>
        candidate.Title == "Create custom dictionary for en_US and add \"smple\"");
      var changedSolution = await GetChangedSolutionAsync(action);
      var customDictionary = changedSolution.GetProject(setup.ProjectId)!.AdditionalDocuments.Single();

      Assert.That(customDictionary.Name, Is.EqualTo("CustomDictionary.en_US.txt"));
      Assert.That(Path.GetFileName(customDictionary.FilePath), Is.EqualTo("CustomDictionary.en_US.txt"));
      Assert.That((await customDictionary.GetTextAsync()).ToString(), Is.EqualTo("smple\n"));
    }

    [Test]
    public async Task DoesNotAddDuplicateWordToCustomDictionary()
    {
      using var workspace = new AdhocWorkspace();
      var setup = await CreateCodeFixContextAsync(workspace, "smple\r\n");

      var action = setup.Actions.Single(candidate =>
        candidate.Title == "Add \"smple\" to custom dictionary for en_US");
      var changedSolution = await GetChangedSolutionAsync(action);
      var customDictionary = changedSolution.GetProject(setup.ProjectId)!.AdditionalDocuments.Single();

      Assert.That((await customDictionary.GetTextAsync()).ToString(), Is.EqualTo("smple\r\n"));
    }

    private static async Task<(ProjectId ProjectId, List<CodeAction> Actions)> CreateCodeFixContextAsync(
      AdhocWorkspace workspace,
      string customDictionaryText)
    {
      var projectId = ProjectId.CreateNewId();
      var documentId = DocumentId.CreateNewId(projectId);
      var projectDirectory = Path.Combine(Path.GetTempPath(), "YouShouldSpellcheck.CodeFixTests");
      var projectFilePath = Path.Combine(projectDirectory, "TestProject.csproj");
      var sourceFilePath = Path.Combine(projectDirectory, "Class1.cs");
      var solution = workspace.CurrentSolution
        .AddProject(ProjectInfo.Create(
          projectId,
          VersionStamp.Create(),
          "TestProject",
          "TestProject",
          LanguageNames.CSharp,
          filePath: projectFilePath,
          parseOptions: CSharpParseOptions.Default))
        .AddDocument(documentId, "Class1.cs", SourceText.From(Source), filePath: sourceFilePath);

      if (customDictionaryText != null)
      {
        solution = solution.AddAdditionalDocument(
          DocumentId.CreateNewId(projectId),
          "CustomDictionary.en_US.txt",
          SourceText.From(customDictionaryText),
          filePath: Path.Combine(projectDirectory, "CustomDictionary.en_US.txt"));
      }

      var document = solution.GetDocument(documentId)!;
      var syntaxTree = await document.GetSyntaxTreeAsync();
      var properties = ImmutableDictionary<string, string>.Empty
        .Add("offendingWord", "smple")
        .Add("validLanguages", "en_US;");
      var descriptor = new DiagnosticDescriptor(
        StringLiteralSpellcheckAnalyzer.StringLiteralDiagnosticId,
        "Spelling",
        "{0}",
        SpellcheckAnalyzerBase.ContentCategory,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
      var diagnostic = Diagnostic.Create(
        descriptor,
        Location.Create(syntaxTree!, new TextSpan(Source.IndexOf("smple", StringComparison.Ordinal), "smple".Length)),
        properties,
        "Possible spelling mistake: smple");
      var actions = new List<CodeAction>();
      var context = new CodeFixContext(
        document,
        diagnostic,
        (action, _) => actions.Add(action),
        CancellationToken.None);

      await new TextCodeFixProvider().RegisterCodeFixesAsync(context);
      return (projectId, actions);
    }

    private static async Task<Solution> GetChangedSolutionAsync(CodeAction action)
    {
      var operations = await action.GetOperationsAsync(CancellationToken.None);
      return operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
    }
  }
}
