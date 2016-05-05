namespace YouShouldSpellcheck.Analyzer
{
  using System.Collections.Generic;
  using System.Collections.Immutable;
  using System.Linq;
  using System.Text.RegularExpressions;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Diagnostics;

  [DiagnosticAnalyzer(LanguageNames.CSharp)]
  public class YouShouldSpellcheckAnalyzer : DiagnosticAnalyzer
  {
    public const string DiagnosticId = "YouShouldSpellcheckAnalyzer";

    // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
    // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
    private const string Title = "Spelling error";
    private const string MessageFormat = "Spelling error: {0}";
    private const string Description = "All text should be spelled correctly.";
    private const string Category = "Naming";

    private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

    // See http://stackoverflow.com/questions/7311734/split-sentence-into-words-but-having-trouble-with-the-punctuations-in-c-sharp
    private Regex splitLineIntoWords = new Regex(@"((\b[^\s]+\b)((?<=\.\w).)?)", RegexOptions.Compiled);

    ////private Regex splitWordsByCasing = new Regex(@"([A-Z]+|[a-z])[a-z]*", RegexOptions.Compiled);
    private Regex splitWordsByCasing = new Regex(@"(\p{Lu}+|\p{Ll})\p{Ll}*", RegexOptions.Compiled);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

    public override void Initialize(AnalysisContext context)
    {
      // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
      // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
      ////context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
      context.RegisterSyntaxNodeAction(AnalyzeStringLiteralToken, SyntaxKind.StringLiteralToken, SyntaxKind.StringLiteralExpression);
      context.RegisterSyntaxNodeAction(AnalyzeVariableDeclarator, SyntaxKind.VariableDeclarator);
      context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
      context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
      context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
      context.RegisterSyntaxNodeAction(AnalyzeXmlText, SyntaxKind.XmlText);
      context.RegisterSyntaxNodeAction(AnalyzeSingleLineCommentTrivia, SyntaxKind.SingleLineCommentTrivia);      
    }

    private void AnalyzeSymbol(SymbolAnalysisContext context)
    {      
      // TODO: Replace the following code with your own analysis, generating Diagnostic objects for any issues you find
      var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

      // Find just those named type symbols with names containing lowercase letters.
      if (namedTypeSymbol.Name.ToCharArray().Any(char.IsLower))
      {
        // For all such symbols, produce a diagnostic.
        var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);

        context.ReportDiagnostic(diagnostic);
      }
    }

    private void AnalyzeStringLiteralToken(SyntaxNodeAnalysisContext context)
    {
      // TODO: use "context.Node.SyntaxTree.FilePath" to find the "custom dictionary"
      var literalExpressionSyntax = context.Node as LiteralExpressionSyntax;
      if (literalExpressionSyntax != null)
      {
        var foo = literalExpressionSyntax.Token;
        var text = foo.ValueText;
        var nodeLocation = literalExpressionSyntax.GetLocation();
        var stringLocation = Location.Create(context.Node.SyntaxTree, Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(nodeLocation.SourceSpan.Start + 1, nodeLocation.SourceSpan.End - 1));

        CheckLine(text, stringLocation, context);
      }
    }

    private void AnalyzeVariableDeclarator(SyntaxNodeAnalysisContext context)
    {
      var variableDeclaratorSyntax = context.Node as VariableDeclaratorSyntax;
      if (variableDeclaratorSyntax != null)
      {
        var identifierToken = variableDeclaratorSyntax.ChildTokens().FirstOrDefault(x => x.IsKind(SyntaxKind.IdentifierToken));
        if (identifierToken != null)
        {
          var text = identifierToken.ValueText;
          CheckWord(text, identifierToken.GetLocation(), context);
        }
      }
    }

    private void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
      var classDeclarationSyntax = context.Node as ClassDeclarationSyntax;
      if (classDeclarationSyntax != null)
      {
        var identifierToken = classDeclarationSyntax.ChildTokens().FirstOrDefault(x => x.IsKind(SyntaxKind.IdentifierToken));
        if (identifierToken != null)
        {
          var text = identifierToken.ValueText;
          CheckWord(text, identifierToken.GetLocation(), context);
        }
      }
    }

    private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
      var methodDeclarationSyntax = context.Node as MethodDeclarationSyntax;
      if (methodDeclarationSyntax != null)
      {
        CheckAllTokensOfType(context, methodDeclarationSyntax, SyntaxKind.IdentifierToken);
      }
    }

    private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
    {
      var propertyDeclarationSyntax = context.Node as PropertyDeclarationSyntax;
      if (propertyDeclarationSyntax != null)
      {
        CheckAllTokensOfType(context, propertyDeclarationSyntax, SyntaxKind.IdentifierToken);
      }
    }

    private void AnalyzeXmlText(SyntaxNodeAnalysisContext context)
    {
      var xmlTextSyntax = context.Node as XmlTextSyntax;
      if (xmlTextSyntax != null)
      {
        CheckAllTokensOfType(context, xmlTextSyntax, SyntaxKind.XmlTextLiteralToken);
      }
    }

    private static void AnalyzeSingleLineCommentTrivia(SyntaxNodeAnalysisContext context)
    {
      if (context.Node != null)
      {
      }
    }

    private void CheckAllTokensOfType(SyntaxNodeAnalysisContext context, SyntaxNode syntaxNode, SyntaxKind syntaxKind)
    {
      foreach (var syntaxToken in syntaxNode.ChildTokens().Where(x => x.IsKind(syntaxKind)))
      {
        var text = syntaxToken.ValueText;
        CheckLine(text, syntaxToken.GetLocation(), context);
      }
    }

    private void CheckLine(string line, Location location, SyntaxNodeAnalysisContext context)
    {
      if (string.IsNullOrWhiteSpace(line))
      {
        return;
      }

      foreach (var wordMatch in splitLineIntoWords.Matches(line).OfType<Match>())
      {
        var wordLocation = Location.Create(context.Node.SyntaxTree, Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(location.SourceSpan.Start + wordMatch.Index, location.SourceSpan.Start + wordMatch.Index + wordMatch.Length));
        CheckWord(wordMatch.Value, wordLocation, context);
      }
    }

    private void CheckWord(string word, Location wordLocation, SyntaxNodeAnalysisContext context)
    {
      var wordParts = splitWordsByCasing.Matches(word).OfType<Match>();
      if (wordParts.Count() < 2)
      {
        CheckWordParts(word, wordLocation, context);
      }
      else
      {
        foreach (var wordPart in wordParts)
        {
          var wordPartLocation = Location.Create(context.Node.SyntaxTree, Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(wordLocation.SourceSpan.Start + wordPart.Index, wordLocation.SourceSpan.Start + wordPart.Index + wordPart.Length));
          CheckWordParts(wordPart.Value, wordPartLocation, context);
        }
      }
    }

    private static void CheckWordParts(string word, Location location, SyntaxNodeAnalysisContext context)
    {
      List<string> suggestions;
      if (!string.IsNullOrWhiteSpace(word) && !IsWordCorrect(word, out suggestions))
      {
        var propertyBagForFixProvider = ImmutableDictionary.Create<string, string>();
        propertyBagForFixProvider = propertyBagForFixProvider.Add("offendingWord", word);
        foreach (var suggestion in suggestions)
        {
          propertyBagForFixProvider = propertyBagForFixProvider.Add("suggestion" + propertyBagForFixProvider.Count, suggestion);
        }

        var diagnostic = Diagnostic.Create(Rule, location, propertyBagForFixProvider, word);

        context.ReportDiagnostic(diagnostic);
      }
    }

    private static bool IsWordCorrect(string word, out List<string> allSuggestions)
    {
      ////return DictionaryManager.IsWordCorrect(word, out allSuggestions, "en_US");

      allSuggestions = null;
      var supportedLanguages = new[] { "en_US", "de_DE_frami" };
      foreach (var language in supportedLanguages)
      {
        List<string> suggestions;
        if (DictionaryManager.IsWordCorrect(word, out suggestions, language))
        {
          allSuggestions = null;
          return true;
        }
        else
        {
          if (allSuggestions == null)
          {
            allSuggestions = new List<string>();
          }

          allSuggestions.AddRange(suggestions);
        }
      }

      return false;
    }
  }
}
