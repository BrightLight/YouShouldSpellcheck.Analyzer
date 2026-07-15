namespace YouShouldSpellcheck.Analyzer.CodeFixes
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CodeActions;
  using Microsoft.CodeAnalysis.CodeFixes;

  public abstract class YouShouldSpellcheckAnalyzerCodeFixProvider : CodeFixProvider
  {
    public abstract override FixAllProvider? GetFixAllProvider();

    protected static bool Suggestions(Diagnostic diagnostic, out string? offendingWord, Dictionary<string, List<string>> allSuggestions, IEnumerable<string> languages)
    {
      if (diagnostic.Properties.TryGetValue("offendingWord", out offendingWord))
      {
        if (offendingWord == null)
        {
          return false;
        }

        var i = 1;
        List<string>? languageToolSuggestions = null;
        while (diagnostic.Properties.TryGetValue($"suggestion_{i}", out var suggestion))
        {
          if (suggestion == null)
          {
            continue;
          }

          if (languageToolSuggestions == null)
          {
            languageToolSuggestions = [];
            allSuggestions.Add("LanguageTool", languageToolSuggestions);
          }

          languageToolSuggestions.Add(suggestion);
          i++;
        }

        if (languageToolSuggestions != null)
        {
          // suggestions from LanguageTool found -> success and return
          return true;
        }

        var localSuggestionIndex = 1;
        while (diagnostic.Properties.TryGetValue($"localSuggestion_{localSuggestionIndex}", out var localSuggestion)
          && diagnostic.Properties.TryGetValue($"localSuggestionLanguage_{localSuggestionIndex}", out var localLanguage))
        {
          if (localSuggestion != null && localLanguage != null)
          {
            if (!allSuggestions.TryGetValue(localLanguage, out var suggestionsForLanguage))
            {
              suggestionsForLanguage = [];
              allSuggestions.Add(localLanguage, suggestionsForLanguage);
            }

            suggestionsForLanguage.Add(localSuggestion);
          }

          localSuggestionIndex++;
        }

        if (localSuggestionIndex > 1)
        {
          return true;
        }

        return false;
      }

      return false;
    }

    protected class NoPreviewCodeAction : CodeAction
    {
      private readonly Func<CancellationToken, Task<Document>> createChangedSolution;

      public override string Title { get; }

      public override string? EquivalenceKey { get; }

      public NoPreviewCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedSolution, string? equivalenceKey = null)
      {
        this.createChangedSolution = createChangedSolution;

        this.Title = title;
        this.EquivalenceKey = equivalenceKey;
      }

      protected override Task<IEnumerable<CodeActionOperation>> ComputePreviewOperationsAsync(CancellationToken cancellationToken)
      {
        return Task.FromResult(Enumerable.Empty<CodeActionOperation>());
      }

      protected override Task<Document> GetChangedDocumentAsync(CancellationToken cancellationToken)
      {
        return this.createChangedSolution(cancellationToken);
      }
    }
  }
}
