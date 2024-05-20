namespace YouShouldSpellcheck.Analyzer.CodeFixes
{
  using System;
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
      StringLiteralSpellcheckAnalyzer.StringLiteralDiagnosticId,
      StringLiteralSpellcheckAnalyzer.AttributeArgumentStringDiagnosticId,
      SpellcheckAnalyzerBase.LanguageToolCasingDiagnosticId,
      SpellcheckAnalyzerBase.LanguageToolColloquialismsDiagnosticId,
      SpellcheckAnalyzerBase.LanguageToolCompoundingDiagnosticId,
      SpellcheckAnalyzerBase.LanguageToolConfusedWordsDiagnosticId,
      SpellcheckAnalyzerBase.LanguageToolFalseFriendsDiagnosticId,
      SpellcheckAnalyzerBase.LanguageToolGenderNeutralityDiagnosticId,
      SpellcheckAnalyzerBase.LanguageToolGrammarDiagnosticId,
      SpellcheckAnalyzerBase.LanguageToolMiscDiagnosticId,
      SpellcheckAnalyzerBase.LanguageToolPunctuationDiagnosticId,
      SpellcheckAnalyzerBase.LanguageToolRedundancyDiagnosticId,
      SpellcheckAnalyzerBase.LanguageToolRegionalismsDiagnosticId,
      SpellcheckAnalyzerBase.LanguageToolRepetitionsDiagnosticId,
      SpellcheckAnalyzerBase.LanguageToolSemanticsDiagnosticId,
      SpellcheckAnalyzerBase.LanguageToolStyleDiagnosticId,
      SpellcheckAnalyzerBase.LanguageToolTypographyDiagnosticId,
      SpellcheckAnalyzerBase.LanguageToolTyposDiagnosticId,
      SpellcheckAnalyzerBase.LanguageToolWikipediaDiagnosticId
    );

    public sealed override FixAllProvider GetFixAllProvider()
    {
      // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
      return WellKnownFixAllProviders.BatchFixer;
    }

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
      var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
      var codeFixCount = 0;

      // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
      foreach (var diagnostic in context.Diagnostics.Where(x => this.FixableDiagnosticIds.Contains(x.Id)))
      {
        var validLanguages = SpellcheckAnalyzerBase.LanguagesByRule(diagnostic.Id).Select(x => x.LocalDictionaryLanguage).ToArray();
        if (diagnostic.Properties.TryGetValue("validLanguages", out var supportedLanguages))
        {
          validLanguages = supportedLanguages?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        if (validLanguages == null)
        {
          continue;
        }

        // Register code actions that will invoke the fix.
        var suggestions = new Dictionary<string, List<string>>();
        if (Suggestions(diagnostic, out var offendingWord, suggestions, validLanguages))
        {
          foreach (var suggestionsForLanguage in suggestions)
          {
            foreach (var suggestion in suggestionsForLanguage.Value)
            {
              var title = $"Replace with ({suggestionsForLanguage.Key}): {suggestion}";
              var codeAction = CodeAction.Create(title, x => this.ReplaceTextAsync(context.Document, diagnostic.Location, suggestion, x), title);
              context.RegisterCodeFix(codeAction, diagnostic);
              codeFixCount++;
            }
          }
        }

        // add "Add to custom dictionary" action
        if (!string.IsNullOrEmpty(offendingWord)
          && diagnostic.Properties.GetValueOrDefault("CategoryId", "n/a") != "TYPOGRAPHY")
        {
          foreach (var language in validLanguages)
          {
            var ignoreSpellingAction = new NoPreviewCodeAction($"Add \"{offendingWord}\" to custom dictionary for {language}", x => this.AddToCustomDictionary(context.Document, offendingWord!, language));
            context.RegisterCodeFix(ignoreSpellingAction, diagnostic);
            codeFixCount++;
          }
        }

        if (codeFixCount == 0)
        {
          var noSuggestionsAction = new NoPreviewCodeAction("No suggestions found", x => Task.FromResult(context.Document));
          context.RegisterCodeFix(noSuggestionsAction, diagnostic);
        }
      }
    }

    protected async Task<Document> ReplaceTextAsync(Document document, Location location, string suggestedWord, CancellationToken cancellationToken)
    {
      var sourceText = await document.GetTextAsync(cancellationToken);
      var newSourceText = sourceText.Replace(location.SourceSpan, suggestedWord);
      var newDocument = document.WithText(newSourceText);

      // Return the new document with replaced text
      return newDocument;
    }
  }
}