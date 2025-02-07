namespace YouShouldSpellcheck.Analyzer.CodeFixes
{
  using System;
  using System.Collections.Generic;
  using System.Globalization;
  using System.Linq;
  using System.Text;
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CodeActions;
  using Microsoft.CodeAnalysis.CodeFixes;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.Rename;

  public abstract class IdentifierCodeFixProvider<T> : YouShouldSpellcheckAnalyzerCodeFixProvider
  {
    public sealed override FixAllProvider GetFixAllProvider()
    {
      // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
      return WellKnownFixAllProviders.BatchFixer;
    }

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
      try
      {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
        {
          return;
        }

        var codeFixCount = 0;
        // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
        foreach (var diagnostic in context.Diagnostics.Where(x => this.FixableDiagnosticIds.Contains(x.Id)))
        {
          // Find the type declaration identified by the diagnostic.
          var diagnosticSpan = diagnostic.Location.SourceSpan;
          ////var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
          var parent = root.FindToken(diagnosticSpan.Start).Parent;
          if (parent == null)
          {
            continue;
          }

          var declaration = parent.AncestorsAndSelf().OfType<T>().FirstOrDefault();
          if (declaration == null)
          {
            continue;
          }

          var validLanguages = SpellcheckAnalyzerBase.LanguagesByRule(diagnostic.Id).Select(x => x.LocalDictionaryLanguage).ToArray();
          if (diagnostic.Properties.TryGetValue("validLanguages", out var supportedLanguages))
          {
            validLanguages = supportedLanguages!.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
          }

          // Register code actions that will invoke the fix.
          var suggestions = new Dictionary<string, List<string>>();
          if (Suggestions(diagnostic, out var offendingWord, suggestions, validLanguages))
          {
            foreach (var suggestionsForLanguage in suggestions)
            {
              foreach (var suggestion in suggestionsForLanguage.Value)
              {
                try
                {
                  var sanitizedSuggestion = SyntaxFacts.IsValidIdentifier(suggestion) ? suggestion : this.MakeCamelCase(suggestion);
                  if (SyntaxFacts.IsValidIdentifier(sanitizedSuggestion))
                  { 
                    // we might only replace part of the identifier, in which case we need to supply the complete new identifier
                    var newIdentifier = sanitizedSuggestion;
                    var identifierToken = this.GetIdentifierToken(declaration);
                    var originalIdentifierSpan = identifierToken.Span;
                    if (originalIdentifierSpan != diagnosticSpan)
                    {
                      var originalIdentifier = identifierToken.Text;
                      newIdentifier = originalIdentifier.Substring(0, diagnosticSpan.Start - originalIdentifierSpan.Start)
                        + sanitizedSuggestion
                        + originalIdentifier.Substring(diagnosticSpan.Start - originalIdentifierSpan.Start + diagnosticSpan.Length);
                    }

                    // Get the symbol representing the type to be renamed.
                    var typeSymbol = await this.GetDeclaredSymbolAsync(context.Document, declaration, context.CancellationToken);
                    if (typeSymbol != null)
                    {
                      var title = $"Replace with ({suggestionsForLanguage.Key}): {newIdentifier}";
                      var codeAction = CodeAction.Create(title, x => this.RenameSymbolAsync(context.Document, typeSymbol, newIdentifier, x), title);
                      context.RegisterCodeFix(codeAction, diagnostic);
                      codeFixCount++;
                    }
                  }
                }
                catch (Exception e)
                {
                  Logger.Log(e);
                }
              }
            }
          }

          if (!string.IsNullOrEmpty(offendingWord))
          {
            // add "Add to custom dictionary" action
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
      catch (Exception e)
      {
        Logger.Log(e.Message);
      }
    }

    protected abstract SyntaxToken GetIdentifierToken(T declarationToken);

    protected abstract Task<ISymbol?> GetDeclaredSymbolAsync(Document document, T typeDecl, CancellationToken cancellationToken);

    private async Task<Solution> RenameSymbolAsync(Document document, ISymbol identifierSymbol, string suggestedWord, CancellationToken cancellationToken)
    {
      // Produce a new solution that has all references to that type renamed, including the declaration.
      var renameOptions = new SymbolRenameOptions
      {
        RenameOverloads = true,
        RenameInStrings = false,
        RenameInComments = true,
        RenameFile = false,
      };
      var newSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, identifierSymbol, renameOptions, suggestedWord, cancellationToken).ConfigureAwait(false);

      // Return the new solution with the now-uppercase type name.
      return newSolution;
    }

    /// <summary>
    /// Checks if the specified <paramref name="suggestedIdentifierName"/> is a compound word,
    /// like "upper class" or "upper-class", which would not be valid identifier names,
    /// in which case we remove the extra " " or "-" and let every word start with an uppercase character.
    /// </summary>
    /// <param name="suggestedIdentifierName">An identifier name as suggested by the spellchecker engine.</param>
    /// <returns>The <paramref name="suggestedIdentifierName"/> without any "-" or " ".</returns>
    /// <remarks>
    /// This method currently supports one space (" ") and one dash ("-") as a separation character.
    /// </remarks>
    private string MakeCamelCase(string suggestedIdentifierName)
    {
      if (suggestedIdentifierName.Contains('-')
        || suggestedIdentifierName.Contains(' '))
      {
        var titleCasedSuggestion = new StringBuilder();
        var parts = suggestedIdentifierName.Split('-', ' ');
        foreach (var part in parts)
        {
          var titleCasedPart = CultureInfo.CurrentUICulture.TextInfo.ToTitleCase(part);
          titleCasedSuggestion.Append(titleCasedPart);
        }

        return titleCasedSuggestion.ToString();
      }

      return suggestedIdentifierName;
    }
  }
}