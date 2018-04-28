namespace YouShouldSpellcheck.Analyzer
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
    protected async Task<Document> AddToCustomDictionary(Document document, string wordToIgnore, string language)
    {
      DictionaryManager.AddToCustomDictionary(wordToIgnore, language);
      return document;
    }

    protected static bool Suggestions(string word, string[] languages, out Dictionary<string, List<string>> allSuggestions)
    {
      allSuggestions = null;
      List<string> suggestionsForLanguage = null;
      foreach (var language in languages)
      {
        List<string> suggestions;
        if (DictionaryManager.Suggest(word, out suggestions, language))
        {
          if (allSuggestions == null)
          {
            allSuggestions = new Dictionary<string, List<string>>();
          }

          if (suggestionsForLanguage == null)
          {
            suggestionsForLanguage = new List<string>();
            allSuggestions.Add(language, suggestionsForLanguage);
          }

          suggestionsForLanguage.AddRange(suggestions);
        }
      }

      return allSuggestions != null;
    }

    protected class NoPreviewCodeAction : CodeAction
    {
      private readonly Func<CancellationToken, Task<Document>> createChangedSolution;

      public override string Title { get; }

      public override string EquivalenceKey { get; }

      public NoPreviewCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedSolution, string equivalenceKey = null)
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