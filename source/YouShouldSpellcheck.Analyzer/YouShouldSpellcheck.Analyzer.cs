namespace YouShouldSpellcheck.Analyzer
{
  using System;
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
    public const string AttributeArgumentStringDiagnosticId = "YS100";

    public const string StringLiteralDiagnosticId = "YS101";

    public const string VariableNameDiagnosticId = "YS102";

    public const string ClassNameDiagnosticId = "YS103";

    public const string MethodNameDiagnosticId = "YS104";

    public const string PropertyNameDiagnosticId = "YS105";

    public const string CommentDiagnosticId = "YS106";

    // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
    // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
    private const string Title = "Spelling error";

    private const string MessageFormat = "Spelling error: {0}";

    private const string AttributeArgumentRuleDescription = "Attribute argument should be spelled correctly.";

    private const string StringLiteralRuleDescription = "All text should be spelled correctly.";

    private const string VariableNameRuleDescription = "Variable name should be spelled correctly.";

    private const string MethodNameRuleDescription = "Method name should be spelled correctly.";

    private const string ClassNameRuleDescription = "Class name should be spelled correctly.";

    private const string PropertyNameRuleDescription = "Property name should be spelled correctly.";

    private const string CommentRuleDescription = "Comment should be spelled correctly.";

    private const string NamingCategory = "Naming";

    private const string CommentCategory = "Comment";

    private const string ContentCategory = "Content";

    private static readonly DiagnosticDescriptor AttributeArgumentStringRule = new DiagnosticDescriptor(AttributeArgumentStringDiagnosticId, Title, MessageFormat, ContentCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: AttributeArgumentRuleDescription);

    private static readonly DiagnosticDescriptor StringLiteralRule = new DiagnosticDescriptor(StringLiteralDiagnosticId, Title, MessageFormat, ContentCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: StringLiteralRuleDescription);

    private static readonly DiagnosticDescriptor VariableNameRule = new DiagnosticDescriptor(VariableNameDiagnosticId, Title, MessageFormat, NamingCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: VariableNameRuleDescription);

    private static readonly DiagnosticDescriptor ClassNameRule = new DiagnosticDescriptor(ClassNameDiagnosticId, Title, MessageFormat, NamingCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: ClassNameRuleDescription);

    private static readonly DiagnosticDescriptor MethodNameRule = new DiagnosticDescriptor(MethodNameDiagnosticId, Title, MessageFormat, NamingCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: MethodNameRuleDescription);

    private static readonly DiagnosticDescriptor PropertyNameRule = new DiagnosticDescriptor(PropertyNameDiagnosticId, Title, MessageFormat, NamingCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: PropertyNameRuleDescription);

    private static readonly DiagnosticDescriptor CommentRule = new DiagnosticDescriptor(CommentDiagnosticId, Title, MessageFormat, CommentCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: CommentRuleDescription);

    // See http://stackoverflow.com/questions/7311734/split-sentence-into-words-but-having-trouble-with-the-punctuations-in-c-sharp
    private readonly Regex splitLineIntoWords = new Regex(@"((\b[^\s.]+\b)((?<=\.\w).)?)", RegexOptions.Compiled);

    ////private Regex splitWordsByCasing = new Regex(@"([A-Z]+|[a-z])[a-z]*", RegexOptions.Compiled);
    private readonly Regex splitWordsByCasing = new Regex(@"(\p{Lu}+|\p{Ll})\p{Ll}*", RegexOptions.Compiled);

    private readonly Regex isGuid = new Regex(@"[{(]?[0-9A-Fa-f]{8}[-]?([0-9A-Fa-f]{4}[-]?){3}[0-9A-Fa-f]{12}[)}]?", RegexOptions.Compiled);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(AttributeArgumentStringRule, StringLiteralRule, VariableNameRule, ClassNameRule, MethodNameRule, PropertyNameRule, CommentRule);

    public override void Initialize(AnalysisContext context)
    {
      context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
      ////context.EnableConcurrentExecution();

      // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
      // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
      ////context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
      context.RegisterSyntaxNodeAction(this.AnalyzeStringLiteralToken, SyntaxKind.StringLiteralToken, SyntaxKind.StringLiteralExpression);
      context.RegisterSyntaxNodeAction(this.AnalyzeVariableDeclarator, SyntaxKind.VariableDeclarator);
      context.RegisterSyntaxNodeAction(this.AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
      context.RegisterSyntaxNodeAction(this.AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
      context.RegisterSyntaxNodeAction(this.AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
      context.RegisterSyntaxNodeAction(this.AnalyzeXmlText, SyntaxKind.XmlText);
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
        var diagnostic = Diagnostic.Create(CommentRule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);

        context.ReportDiagnostic(diagnostic);
      }
    }

    private void AnalyzeStringLiteralToken(SyntaxNodeAnalysisContext context)
    {
      try
      {
        var attributeArgumentSyntax = context.Node?.Parent as AttributeArgumentSyntax;
        if (attributeArgumentSyntax != null)
        {
          this.AnalyzeAttributeArgument(context, attributeArgumentSyntax);
        }
        else
        {
          // TODO: use "context.Node.SyntaxTree.FilePath" to find the "custom dictionary"
          var literalExpressionSyntax = context.Node as LiteralExpressionSyntax;
          if (literalExpressionSyntax != null)
          {
            var foo = literalExpressionSyntax.Token;
            var text = foo.ValueText;
            var nodeLocation = literalExpressionSyntax.GetLocation();
            var stringLocation = Location.Create(context.Node.SyntaxTree, Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(nodeLocation.SourceSpan.Start + 1, nodeLocation.SourceSpan.End - 1));

            this.CheckLine(StringLiteralRule, text, stringLocation, context);
          }
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }
    }

    private void AnalyzeAttributeArgument(SyntaxNodeAnalysisContext context, AttributeArgumentSyntax attributeArgumentSyntax)
    {
      var attributeSyntax = attributeArgumentSyntax.Parent?.Parent as AttributeSyntax;
      if (attributeSyntax != null)
      {
        var attributeName = attributeSyntax.Name.ToFullString();
        if (!SpellcheckSettings.InspectedAttributes.Contains(attributeName))
        {
          return;
        }

        var attributeArgumentName = this.DetermineAttributeArgumentName(context.SemanticModel, attributeArgumentSyntax);
        if (attributeArgumentName == null)
        {
          return;
        }

        if (SpellcheckSettings.CheckAttributeArgument(attributeName, attributeArgumentName))
        {
          // next lines are identical to the ones in AnalyzeStringLiteralToken.
          // this will be resolved ones we have one class per analyzer and can use inheritance to override stuff
          // TODO: use "context.Node.SyntaxTree.FilePath" to find the "custom dictionary"
          var literalExpressionSyntax = context.Node as LiteralExpressionSyntax;
          if (literalExpressionSyntax != null)
          {
            var foo = literalExpressionSyntax.Token;
            var text = foo.ValueText;
            var nodeLocation = literalExpressionSyntax.GetLocation();
            var stringLocation = Location.Create(context.Node.SyntaxTree, Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(nodeLocation.SourceSpan.Start + 1, nodeLocation.SourceSpan.End - 1));

            this.CheckLine(StringLiteralRule, text, stringLocation, context);
          }
        }
      }
    }

    private string DetermineAttributeArgumentName(SemanticModel semanticModel, AttributeArgumentSyntax attributeArgumentSyntax)
    {
      // check if syntax represents a named argument
      // -> name is specified directly with the argument
      if (attributeArgumentSyntax.NameEquals != null)
      {
        return attributeArgumentSyntax.NameEquals.Name.Identifier.ValueText;
      }

      // syntax does not represent a named argument
      // -> we need to find suitable construtor to deduct argument name
      var attributeSyntax = attributeArgumentSyntax.Parent?.Parent as AttributeSyntax;
      if (attributeSyntax != null)
      {
        var attributeTypeInfo = semanticModel.GetTypeInfo(attributeSyntax);
        var namedTypeSymbol = attributeTypeInfo.Type as INamedTypeSymbol;
        if (namedTypeSymbol != null)
        {
          var nonNamedArguments = attributeSyntax.ArgumentList.Arguments.Where(x => x.NameEquals == null).ToList();
          foreach (var constructorDefinition in namedTypeSymbol.InstanceConstructors)
          {
            var constructorArguments = constructorDefinition.Parameters.ToList();
            if (constructorArguments.Count >= nonNamedArguments.Count)
            {              
              for (var i = 0; i < nonNamedArguments.Count; i++)
              {
                //if (constructorArguments[i].Type == nonNamedArguments[i].
              }
            }
          }
        }
      }

      return null;
    }

    private void AnalyzeVariableDeclarator(SyntaxNodeAnalysisContext context)
    {
      try
      {
        var variableDeclaratorSyntax = context.Node as VariableDeclaratorSyntax;
        if (variableDeclaratorSyntax != null)
        {
          var identifierToken = variableDeclaratorSyntax.ChildTokens().FirstOrDefault(x => x.IsKind(SyntaxKind.IdentifierToken));
          if (identifierToken != null)
          {
            var text = identifierToken.ValueText;
            this.CheckWord(VariableNameRule, text, identifierToken.GetLocation(), context);
          }
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }
    }

    private void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
      try
      {
        var classDeclarationSyntax = context.Node as ClassDeclarationSyntax;
        if (classDeclarationSyntax != null)
        {
          var identifierToken = classDeclarationSyntax.ChildTokens().FirstOrDefault(x => x.IsKind(SyntaxKind.IdentifierToken));
          if (identifierToken != null)
          {
            var text = identifierToken.ValueText;
            this.CheckWord(ClassNameRule, text, identifierToken.GetLocation(), context);
          }
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }
    }

    private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
      try
      {
        var methodDeclarationSyntax = context.Node as MethodDeclarationSyntax;
        if (methodDeclarationSyntax != null)
        {
          this.CheckAllTokensOfType(MethodNameRule, context, methodDeclarationSyntax, SyntaxKind.IdentifierToken);
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }
    }

    private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
    {
      try
      {
        var propertyDeclarationSyntax = context.Node as PropertyDeclarationSyntax;
        if (propertyDeclarationSyntax != null)
        {
          this.CheckAllTokensOfType(PropertyNameRule, context, propertyDeclarationSyntax, SyntaxKind.IdentifierToken);
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }
    }

    private void AnalyzeXmlText(SyntaxNodeAnalysisContext context)
    {
      try
      {
        var xmlTextSyntax = context.Node as XmlTextSyntax;
        if (xmlTextSyntax != null)
        {
          this.CheckAllTokensOfType(CommentRule, context, xmlTextSyntax, SyntaxKind.XmlTextLiteralToken);
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }
    }

    private static void AnalyzeSingleLineCommentTrivia(SyntaxNodeAnalysisContext context)
    {
      if (context.Node != null)
      {
      }
    }

    private void CheckAllTokensOfType(DiagnosticDescriptor rule, SyntaxNodeAnalysisContext context, SyntaxNode syntaxNode, SyntaxKind syntaxKind)
    {
      foreach (var syntaxToken in syntaxNode.ChildTokens().Where(x => x.IsKind(syntaxKind)))
      {
        var text = syntaxToken.ValueText;
        if (string.IsNullOrWhiteSpace(text))
        {
          continue;
        }

        this.CheckLine(rule, text, syntaxToken.GetLocation(), context);
      }
    }

    private void CheckLine(DiagnosticDescriptor rule, string line, Location location, SyntaxNodeAnalysisContext context)
    {
      if (string.IsNullOrWhiteSpace(line))
      {
        return;
      }

      foreach (var wordMatch in this.splitLineIntoWords.Matches(line).OfType<Match>())
      {
        var wordLocation = Location.Create(context.Node.SyntaxTree, Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(location.SourceSpan.Start + wordMatch.Index, location.SourceSpan.Start + wordMatch.Index + wordMatch.Length));
        this.CheckWord(rule, wordMatch.Value, wordLocation, context);
      }
    }

    private void CheckWord(DiagnosticDescriptor rule, string word, Location wordLocation, SyntaxNodeAnalysisContext context)
    {
      // check if the whole "word" with exactly that casing is configured as a custom word (e.g. "HiFi")
      if (DictionaryManager.IsCustomWord(word))
      {
        return;
      }

      // check if the "word" actually represents a GUID which should not further be parsed
      if (this.isGuid.IsMatch(word))
      {
        return;
      }

      var wordParts = this.splitWordsByCasing.Matches(word).OfType<Match>();
      foreach (var wordPart in wordParts)
      {
        var wordPartLocation = Location.Create(context.Node.SyntaxTree, Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(wordLocation.SourceSpan.Start + wordPart.Index, wordLocation.SourceSpan.Start + wordPart.Index + wordPart.Length));
        CheckWordParts(rule, wordPart.Value, wordPartLocation, context);
      }
    }

    private static void CheckWordParts(DiagnosticDescriptor rule, string word, Location location, SyntaxNodeAnalysisContext context)
    {
      if (!string.IsNullOrWhiteSpace(word)
        && !IsWordCorrect(word, LanguagesByRule(rule.Id)))
      {
        var propertyBagForFixProvider = ImmutableDictionary.Create<string, string>();
        propertyBagForFixProvider = propertyBagForFixProvider.Add("offendingWord", word);
        var diagnostic = Diagnostic.Create(rule, location, propertyBagForFixProvider, word);
        context.ReportDiagnostic(diagnostic);
      }
    }

    private static bool IsWordCorrect(string word, string[] languages)
    {
      return languages.Any(language => DictionaryManager.IsWordCorrect(word, language));
    }

    public static string[] LanguagesByRule(string ruleId)
    {
      switch (ruleId)
      {
        case ClassNameDiagnosticId: return SpellcheckSettings.ClassNameLanguagses;
        case MethodNameDiagnosticId: return SpellcheckSettings.MethodNameLanguagses;
        case VariableNameDiagnosticId: return SpellcheckSettings.VariableNameLanguagses;
        case PropertyNameDiagnosticId: return SpellcheckSettings.PropertyNameLanguagses;
        case AttributeArgumentStringDiagnosticId: return SpellcheckSettings.AttributeArgumentLanguages;
        case CommentDiagnosticId: return SpellcheckSettings.CommentLanguagses;
        case StringLiteralDiagnosticId: return SpellcheckSettings.StringLiteralLanguages;
        default: return SpellcheckSettings.DefaultLanguages;
      }
    }
  }
}