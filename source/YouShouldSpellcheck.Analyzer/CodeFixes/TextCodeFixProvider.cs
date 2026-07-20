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

  [ExportCodeFixProvider(LanguageNames.CSharp, Name = "YouShouldSpellcheck.TextCodeFixProvider"), Shared]
  public class TextCodeFixProvider : YouShouldSpellcheckAnalyzerCodeFixProvider
  {
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
      XmlTextSpellcheckAnalyzer.CommentDiagnosticId,
      StringLiteralSpellcheckAnalyzer.StringLiteralDiagnosticId,
      StringLiteralSpellcheckAnalyzer.AttributeArgumentStringDiagnosticId
    );

    public sealed override FixAllProvider GetFixAllProvider()
    {
      // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
      return WellKnownFixAllProviders.BatchFixer;
    }

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
      await RegisterTextCodeFixesAsync(context, this.FixableDiagnosticIds).ConfigureAwait(false);
    }

    internal static Task RegisterTextCodeFixesAsync(
      CodeFixContext context,
      ImmutableArray<string> fixableDiagnosticIds) =>
      RegisterTextCodeFixesCoreAsync(context, fixableDiagnosticIds);

    private static async Task RegisterTextCodeFixesCoreAsync(
      CodeFixContext context,
      ImmutableArray<string> fixableDiagnosticIds)
    {
      await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

      foreach (var diagnostic in context.Diagnostics.Where(x => fixableDiagnosticIds.Contains(x.Id)))
      {
        var codeFixCount = 0;
        var validLanguages = Array.Empty<string>();
        if (diagnostic.Properties.TryGetValue("validLanguages", out var supportedLanguages))
        {
          validLanguages = supportedLanguages?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        if (validLanguages == null)
        {
          continue;
        }

        var suggestions = new Dictionary<string, List<string>>();
        if (Suggestions(diagnostic, out var offendingWord, suggestions, validLanguages))
        {
          foreach (var suggestionsForLanguage in suggestions)
          {
            foreach (var suggestion in suggestionsForLanguage.Value)
            {
              var title = $"Replace with ({suggestionsForLanguage.Key}): {suggestion}";
              var codeAction = CodeAction.Create(title, x => ReplaceTextAsync(context.Document, diagnostic.Location, suggestion, x), title);
              context.RegisterCodeFix(codeAction, diagnostic);
              codeFixCount++;
            }
          }
        }

        codeFixCount += RegisterCustomDictionaryCodeFixes(context, diagnostic, offendingWord, validLanguages);

        if (codeFixCount == 0)
        {
          var noSuggestionsAction = new NoPreviewCodeAction("No suggestions found", x => Task.FromResult(context.Document));
          context.RegisterCodeFix(noSuggestionsAction, diagnostic);
        }
      }
    }

    protected static async Task<Document> ReplaceTextAsync(Document document, Location location, string suggestedWord, CancellationToken cancellationToken)
    {
      var sourceText = await document.GetTextAsync(cancellationToken);
      var newSourceText = sourceText.Replace(location.SourceSpan, suggestedWord);
      var newDocument = document.WithText(newSourceText);

      return newDocument;
    }
  }

  [ExportCodeFixProvider(LanguageNames.CSharp, Name = "YouShouldSpellcheck.LanguageToolCodeFixProvider"), Shared]
  public sealed class LanguageToolCodeFixProvider : YouShouldSpellcheckAnalyzerCodeFixProvider
  {
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
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

    public override FixAllProvider GetFixAllProvider()
    {
      // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
      return WellKnownFixAllProviders.BatchFixer;
    }

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
      return TextCodeFixProvider.RegisterTextCodeFixesAsync(context, this.FixableDiagnosticIds);
    }
  }
}
