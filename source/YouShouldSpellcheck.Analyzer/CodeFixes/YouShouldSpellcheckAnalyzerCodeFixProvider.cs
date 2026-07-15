namespace YouShouldSpellcheck.Analyzer.CodeFixes
{
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Text;
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CodeActions;
  using Microsoft.CodeAnalysis.CodeFixes;
  using Microsoft.CodeAnalysis.Text;

  public abstract class YouShouldSpellcheckAnalyzerCodeFixProvider : CodeFixProvider
  {
    public abstract override FixAllProvider? GetFixAllProvider();

    protected static int RegisterCustomDictionaryCodeFixes(
      CodeFixContext context,
      Diagnostic diagnostic,
      string? offendingWord,
      IEnumerable<string> languages)
    {
      if (string.IsNullOrWhiteSpace(offendingWord)
        || diagnostic.Id.StartsWith("YS2", StringComparison.Ordinal))
      {
        return 0;
      }

      var word = offendingWord!;
      var registeredActions = 0;
      foreach (var language in languages
        .Where(language => !string.IsNullOrWhiteSpace(language))
        .Distinct(StringComparer.OrdinalIgnoreCase))
      {
        var fileName = $"CustomDictionary.{language}.txt";
        var customDictionary = FindCustomDictionary(context.Document.Project, fileName);
        string title;
        CodeAction codeAction;
        if (customDictionary != null)
        {
          title = $"Add \"{word}\" to custom dictionary for {language}";
          codeAction = CodeAction.Create(
            title,
            cancellationToken => AddWordAsync(customDictionary, word, cancellationToken),
            title);
        }
        else
        {
          var filePath = GetNewCustomDictionaryPath(context.Document, fileName);
          if (filePath == null)
          {
            continue;
          }

          title = $"Create custom dictionary for {language} and add \"{word}\"";
          codeAction = CodeAction.Create(
            title,
            cancellationToken => CreateCustomDictionaryAsync(
              context.Document.Project,
              fileName,
              filePath,
              word,
              cancellationToken),
            title);
        }

        context.RegisterCodeFix(codeAction, diagnostic);
        registeredActions++;
      }

      return registeredActions;
    }

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

    private static TextDocument? FindCustomDictionary(Project project, string fileName) =>
      project.AdditionalDocuments.FirstOrDefault(document =>
        string.Equals(Path.GetFileName(document.FilePath ?? document.Name), fileName, StringComparison.OrdinalIgnoreCase));

    private static string? GetNewCustomDictionaryPath(Document document, string fileName)
    {
      var projectDirectory = Path.GetDirectoryName(document.Project.FilePath);
      if (string.IsNullOrEmpty(projectDirectory))
      {
        projectDirectory = Path.GetDirectoryName(document.FilePath);
      }

      return string.IsNullOrEmpty(projectDirectory) ? null : Path.Combine(projectDirectory, fileName);
    }

    private static async Task<Solution> AddWordAsync(
      TextDocument customDictionary,
      string word,
      CancellationToken cancellationToken)
    {
      var text = await customDictionary.GetTextAsync(cancellationToken).ConfigureAwait(false);
      if (text.Lines.Any(line => string.Equals(line.ToString().Trim(), word, StringComparison.Ordinal)))
      {
        return customDictionary.Project.Solution;
      }

      var lineBreak = GetLineBreak(text);
      var currentText = text.ToString();
      var separator = currentText.Length == 0
        || currentText.EndsWith("\r", StringComparison.Ordinal)
        || currentText.EndsWith("\n", StringComparison.Ordinal)
          ? string.Empty
          : lineBreak;
      var newText = text.WithChanges(new TextChange(new TextSpan(text.Length, 0), separator + word + lineBreak));
      return customDictionary.Project.Solution.WithAdditionalDocumentText(customDictionary.Id, newText);
    }

    private static async Task<Solution> CreateCustomDictionaryAsync(
      Project project,
      string fileName,
      string filePath,
      string word,
      CancellationToken cancellationToken)
    {
      var existingDictionary = FindCustomDictionary(project, fileName);
      if (existingDictionary != null)
      {
        return await AddWordAsync(existingDictionary, word, cancellationToken).ConfigureAwait(false);
      }

      cancellationToken.ThrowIfCancellationRequested();
      var documentId = DocumentId.CreateNewId(project.Id, fileName);
      var text = SourceText.From(word + "\n", Encoding.UTF8);
      return project.Solution.AddAdditionalDocument(documentId, fileName, text, filePath: filePath);
    }

    private static string GetLineBreak(SourceText text)
    {
      foreach (var line in text.Lines)
      {
        if (line.EndIncludingLineBreak > line.End)
        {
          return text.ToString(TextSpan.FromBounds(line.End, line.EndIncludingLineBreak));
        }
      }

      return "\n";
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
