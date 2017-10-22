﻿namespace YouShouldSpellcheck.Analyzer
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
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Rename;

  [ExportCodeFixProvider(LanguageNames.CSharp, "", Name = nameof(YouShouldSpellcheckAnalyzerCodeFixProvider)), Shared]
  public class YouShouldSpellcheckAnalyzerCodeFixProvider : CodeFixProvider
  {
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
      SpellcheckAnalyzerBase.AttributeArgumentStringDiagnosticId,
      ClassNameSpellcheckAnalyzer.ClassNameDiagnosticId,
      XmlTextSpellcheckAnalyzer.CommentDiagnosticId,
      MethodNameSpellcheckAnalyzer.MethodNameDiagnosticId,
      PropertyNameSpellcheckAnalyzer.PropertyNameDiagnosticId,
      StringLiteralSpellcheckAnalyzer.StringLiteralDiagnosticId,
      VariableNameSpellcheckAnalyzer.VariableNameDiagnosticId);

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
          List<string> suggestions;
          if (Suggestions(offendingWord, SpellcheckAnalyzerBase.LanguagesByRule(diagnostic.Id), out suggestions))
          {
            foreach (var suggestion in suggestions)
            {
              var title = $"Replace with: {suggestion}";
              var codeAction = CodeAction.Create(title, x => this.ReplaceText(context.Document, diagnostic.Location, suggestion, x), title);
              context.RegisterCodeFix(codeAction, diagnostic);
            }
          }

          // add "Add to custom dictionary" action
          foreach (var language in SpellcheckAnalyzerBase.LanguagesByRule(diagnostic.Id))
          {
            var ignoreSpellingAction = new NoPreviewCodeAction($"Add \"{offendingWord}\" to custom dictionary for {language}", x => this.AddToCustomDictionary(context.Document, offendingWord, language));
            context.RegisterCodeFix(ignoreSpellingAction, diagnostic);
          }
        }
      }
    }

    private async Task<Document> AddToCustomDictionary(Document document, string wordToIgnore, string language)
    {
      DictionaryManager.AddToCustomDictionary(wordToIgnore, language);
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

    ////private async Task<Solution> RenameSymbol(Document document, Location location, string suggestedWord, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
    ////{
    ////  // Get the symbol representing the type to be renamed.
    ////  var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
    ////  var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

    ////  // Produce a new solution that has all references to that type renamed, including the declaration.
    ////  var originalSolution = document.Project.Solution;
    ////  var optionSet = originalSolution.Workspace.Options;
    ////  var newSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, typeSymbol, suggestedWord, optionSet, cancellationToken).ConfigureAwait(false);

    ////  // Return the new solution with the now-uppercase type name.
    ////  return newSolution;
    ////}

    private static bool Suggestions(string word, string[] languages, out List<string> allSuggestions)
    {
      allSuggestions = null;
      foreach (var language in languages)
      {
        List<string> suggestions;
        if (DictionaryManager.Suggest(word, out suggestions, language))
        {
          if (allSuggestions == null)
          {
            allSuggestions = new List<string>();
          }

          allSuggestions.AddRange(suggestions);
        }
      }

      return allSuggestions != null;
    }

    private class NoPreviewCodeAction : CodeAction
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