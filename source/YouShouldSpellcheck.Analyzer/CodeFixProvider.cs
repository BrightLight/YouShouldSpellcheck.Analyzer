namespace YouShouldSpellcheck.Analyzer
{
  using System.Collections.Immutable;
  using System.Composition;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CodeFixes;
  using Microsoft.CodeAnalysis.CodeActions;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Rename;

  [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(YouShouldSpellcheckAnalyzerCodeFixProvider)), Shared]
  public class YouShouldSpellcheckAnalyzerCodeFixProvider : CodeFixProvider
  {
    public sealed override ImmutableArray<string> FixableDiagnosticIds
    {
      get { return ImmutableArray.Create(YouShouldSpellcheckAnalyzer.DiagnosticId); }
    }

    public sealed override FixAllProvider GetFixAllProvider()
    {
      // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
      return WellKnownFixAllProviders.BatchFixer;
    }

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
      var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

      // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
      foreach (var diagnostic in context.Diagnostics.Where(x => x.Id == YouShouldSpellcheckAnalyzer.DiagnosticId))
      {
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the type declaration identified by the diagnostic.
        // var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();
        TypeDeclarationSyntax declaration = null;

        // Register a code action that will invoke the fix.
        ////context.RegisterCodeFix(
        ////    CodeAction.Create(
        ////        title: title,
        ////        createChangedSolution: c => MakeUppercaseAsync(context.Document, declaration, c),
        ////        equivalenceKey: title),
        ////    diagnostic);
        if (declaration == null)
        {
          foreach (var suggestion in diagnostic.Properties.Where(x => x.Key.StartsWith("suggestion")).OrderBy(x => x.Key).Select(x => x.Value))
          {
            ////var codeAction = CodeAction.Create(string.Format("Replace with: {0}", suggestion), x => ReplaceText(context.Document, diagnostic.Location, suggestion, x), suggestion);
            var codeAction = CodeAction.Create(string.Format("Replace with: {0}", suggestion), x => ReplaceText(context.Document, diagnostic.Location, suggestion, x));
            context.RegisterCodeFix(codeAction, diagnostic);
          }
        }
        else
        {
          foreach (var suggestion in diagnostic.Properties.Where(x => x.Key.StartsWith("suggestion")).OrderBy(x => x.Key).Select(x => x.Value))
          {
            var codeAction = CodeAction.Create(suggestion, x => RenameSymbol(context.Document, diagnostic.Location, suggestion, declaration, x), suggestion);
            context.RegisterCodeFix(codeAction, diagnostic);
          }
        }

        // add "Ignore" action
        string offendingWord;
        if (diagnostic.Properties.TryGetValue("offendingWord", out offendingWord))
        {
          var ignoreSpellingAction = CodeAction.Create(string.Format("Ignore spelling for \"{0}\"", offendingWord), x => IgnoreWord(context.Document, offendingWord));
          context.RegisterCodeFix(ignoreSpellingAction, diagnostic);
        }
      }
    }

    private async Task<Document> IgnoreWord(Document document, string wordToIgnore)
    {
      DictionaryManager.AddToCustomDictionary(wordToIgnore);
      return document;
    }

    private async Task<Document> ReplaceText(Document document, Location location, string suggestedWord, CancellationToken cancellationToken)
    {
      var sourceText = await document.GetTextAsync(cancellationToken);
      var newSourceText = sourceText.Replace(location.SourceSpan, suggestedWord);
      var newDocument = document.WithText(newSourceText);

      // Return the new document with replaced text
      return newDocument;
    }

    private async Task<Solution> RenameSymbol(Document document, Location location, string suggestedWord, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
    {
      // Get the symbol representing the type to be renamed.
      var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
      var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

      // Produce a new solution that has all references to that type renamed, including the declaration.
      var originalSolution = document.Project.Solution;
      var optionSet = originalSolution.Workspace.Options;
      var newSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, typeSymbol, suggestedWord, optionSet, cancellationToken).ConfigureAwait(false);

      // Return the new solution with the now-uppercase type name.
      return newSolution;
    }
  }
}