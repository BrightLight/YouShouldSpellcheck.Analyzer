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
    protected async Task<Document> AddToCustomDictionary(Document document, string wordToIgnore, string language)
    {
      DictionaryManager.AddToCustomDictionary(wordToIgnore, language);
      return document;
    }

    protected static bool Suggestions(Diagnostic diagnostic, out string offendingWord, Dictionary<string, List<string>> allSuggestions, IEnumerable<string> languages)
    {
      if (diagnostic.Properties.TryGetValue("offendingWord", out offendingWord))
      {
        var i = 1;
        List<string> languageToolSuggestions = null;
        while (diagnostic.Properties.TryGetValue($"suggestion_{i}", out var suggestion))
        {
          if (languageToolSuggestions == null)
          {
            languageToolSuggestions = new List<string>();
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

        // no LanguageTool suggestions -> try to find suggestions using local dictionary
        return Suggestions(offendingWord, languages, allSuggestions);
      }

      return false;
    }

    protected static bool Suggestions(string word, IEnumerable<string> languages, Dictionary<string, List<string>> allSuggestions)
    {
      if ((languages == null)
       || (allSuggestions == null))
      {
        return false;
      }

      foreach (var language in languages)
      {
        if (DictionaryManager.Suggest(word, out var suggestions, language))
        {
          var suggestionsForLanguage = new List<string>();
          allSuggestions.Add(language, suggestionsForLanguage);

          suggestionsForLanguage.AddRange(suggestions);
        }
      }

      return true;
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