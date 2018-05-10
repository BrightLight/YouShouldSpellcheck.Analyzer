namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Generic;
  using System.Collections.Immutable;
  using System.Linq;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Diagnostics;
  using Microsoft.CodeAnalysis.Text;

  [DiagnosticAnalyzer(LanguageNames.CSharp)]
  public class StringLiteralSpellcheckAnalyzer : SpellcheckAnalyzerBase
  {
    public const string StringLiteralDiagnosticId = "YS101";
    private const string StringLiteralRuleTitle = "All text should be spelled correctly.";
    private const string StringLiteralRuleDescription = "All text should be spelled correctly.";
    private static readonly DiagnosticDescriptor StringLiteralRule = new DiagnosticDescriptor(StringLiteralDiagnosticId, StringLiteralRuleTitle, MessageFormat, ContentCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: StringLiteralRuleDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(StringLiteralRule);

    public override void Initialize(AnalysisContext context)
    {
      context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
      context.EnableConcurrentExecution();

      // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
      // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
      context.RegisterSyntaxNodeAction(this.AnalyzeStringLiteralToken, SyntaxKind.StringLiteralToken, SyntaxKind.StringLiteralExpression);
    }

    protected override bool CheckWord(DiagnosticDescriptor rule, string word, Location wordLocation, SyntaxNodeAnalysisContext context, IEnumerable<ILanguage> languages)
    {
      if (!base.CheckWord(rule, word, wordLocation, context, languages))
      {
        ReportWord(rule, word, wordLocation, context);
      }

      return true;
    }

    private void AnalyzeStringLiteralToken(SyntaxNodeAnalysisContext context)
    {
      try
      {
        AnalyzerContext.InitializeSettings(context);
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
            var stringLocation = Location.Create(context.Node.SyntaxTree, TextSpan.FromBounds(nodeLocation.SourceSpan.Start + 1, nodeLocation.SourceSpan.End - 1));

            this.CheckLine(StringLiteralRule, text, stringLocation, context, LanguagesByRule(StringLiteralRule.Id));
          }
        }
      }
      catch (Exception e)
      {
        Logger.Log(e);
        Console.WriteLine(e);
      }
    }

    private void AnalyzeAttributeArgument(SyntaxNodeAnalysisContext context, AttributeArgumentSyntax attributeArgumentSyntax)
    {
      var attributeSyntax = attributeArgumentSyntax.Parent?.Parent as AttributeSyntax;
      if (attributeSyntax != null)
      {
        var attributeName = attributeSyntax.Name.ToFullString();
        var spellcheckSettings = AnalyzerContext.SpellcheckSettings;
        var attributeProperties = spellcheckSettings.Attributes?.Where(x => (x.AttributeName == attributeName) || (x.AttributeName + "Attribute" == attributeName) || (x.AttributeName == attributeName + "Attribute")).ToList();
        if (attributeProperties == null || !attributeProperties.Any())
        {
          return;
        }

        var attributeArgumentName = this.DetermineAttributeArgumentName(context.SemanticModel, attributeArgumentSyntax);
        if (attributeArgumentName == null)
        {
          return;
        }

        var attributePropertyLanguages = attributeProperties.FirstOrDefault(x => string.Equals(x.PropertyName, attributeArgumentName, StringComparison.OrdinalIgnoreCase));
        if (attributePropertyLanguages != null)
        {
          // next lines are identical to the ones in AnalyzeStringLiteralToken.
          // this will be resolved once we have one class per analyzer and can use inheritance to override stuff
          // TODO: use "context.Node.SyntaxTree.FilePath" to find the "custom dictionary"
          var literalExpressionSyntax = context.Node as LiteralExpressionSyntax;
          if (literalExpressionSyntax != null)
          {
            var foo = literalExpressionSyntax.Token;
            var text = foo.ValueText;
            var nodeLocation = literalExpressionSyntax.GetLocation();
            var stringLocation = Location.Create(context.Node.SyntaxTree, TextSpan.FromBounds(nodeLocation.SourceSpan.Start + 1, nodeLocation.SourceSpan.End - 1));

            // try to do a languagetool check
            // if languagetool is not configured, use local dictionary
            if (!CheckTextWithLanguageTool(StringLiteralRule, stringLocation, text, attributePropertyLanguages.Languages, context))
            {
              this.CheckLine(StringLiteralRule, text, stringLocation, context, attributePropertyLanguages.Languages);
            }
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
      // -> we need to find suitable constructor to deduct argument name
      // ToDo this is a very, very simplified "constructor resolution" approach
      // I guess it should be possible, somehow, to ask Roslyn to which compiled the current attribute-argument belongs
      // but I don't know how to do that, hence this "has-to-do-for-now" approach
      var attributeSyntax = attributeArgumentSyntax.Parent?.Parent as AttributeSyntax;
      if (attributeSyntax != null)
      {
        var attributeTypeInfo = semanticModel.GetTypeInfo(attributeSyntax);
        var namedTypeSymbol = attributeTypeInfo.Type as INamedTypeSymbol;
        if (namedTypeSymbol != null)
        {
          var nonNamedArguments = attributeSyntax.ArgumentList.Arguments.Where(x => x.NameEquals == null).ToList();
          var attributeArgumentIndex = nonNamedArguments.FindIndex(x => x == attributeArgumentSyntax);
          foreach (var constructorDefinition in namedTypeSymbol.InstanceConstructors)
          {
            var constructorArguments = constructorDefinition.Parameters.ToList();
            if (constructorArguments.Count >= nonNamedArguments.Count)
            {
              return constructorArguments[attributeArgumentIndex].Name;
              ////for (var i = 0; i < nonNamedArguments.Count; i++)
              ////{
              ////  //if (constructorArguments[i].Type == nonNamedArguments[i].
              ////}
            }
          }
        }
      }

      return null;
    }
  }
}
