namespace YouShouldSpellcheck.Analyzer
{
  using System.Collections.Generic;
  using System.Collections.Immutable;
  using System.Composition;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CodeFixes;
  using Microsoft.CodeAnalysis.CodeActions;

  [ExportCodeFixProvider(LanguageNames.CSharp, "", Name = nameof(TextCodeFixProvider)), Shared]
  public class TextCodeFixProvider : YouShouldSpellcheckAnalyzerCodeFixProvider
  {
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
      XmlTextSpellcheckAnalyzer.CommentDiagnosticId,
      StringLiteralSpellcheckAnalyzer.StringLiteralDiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider()
    {
      // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
      return WellKnownFixAllProviders.BatchFixer;
    }

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
      var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

      // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
      foreach (var diagnostic in context.Diagnostics.Where(x => this.FixableDiagnosticIds.Contains(x.Id)))
      {
        // Register code actions that will invoke the fix.
        string offendingWord;
        if (diagnostic.Properties.TryGetValue("offendingWord", out offendingWord))
        {
          Dictionary<string, List<string>> suggestions;
          if (Suggestions(offendingWord, SpellcheckAnalyzerBase.LanguagesByRule(diagnostic.Id), out suggestions))
          {
            foreach (var suggestionsForLanguage in suggestions)
            {
              foreach (var suggestion in suggestionsForLanguage.Value)
              {
                var title = $"Replace with ({suggestionsForLanguage.Key}): {suggestion}";
                var codeAction = CodeAction.Create(title, x => this.ReplaceText(context.Document, diagnostic.Location, suggestion, x), title);
                context.RegisterCodeFix(codeAction, diagnostic);
              }
            }
          }

          // add "Add to custom dictionary" action
          foreach (var language in SpellcheckAnalyzerBase.LanguagesByRule(diagnostic.Id).Select(x => x.LocalDictionaryLanguage))
          {
            var ignoreSpellingAction = new NoPreviewCodeAction($"Add \"{offendingWord}\" to custom dictionary for {language}", x => this.AddToCustomDictionary(context.Document, offendingWord, language));
            context.RegisterCodeFix(ignoreSpellingAction, diagnostic);
          }
        }
      }
    }

    protected async Task<Document> ReplaceText(Document document, Location location, string suggestedWord, CancellationToken cancellationToken)
    {
      var sourceText = await document.GetTextAsync(cancellationToken);
      var newSourceText = sourceText.Replace(location.SourceSpan, suggestedWord);
      var newDocument = document.WithText(newSourceText);

      // Return the new document with replaced text
      return newDocument;
    }
  }
}