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

  public sealed class StringLiteralSpellcheckAnalyzer : SpellcheckAnalyzerBase
  {
    public const string AttributeArgumentStringDiagnosticId = "YS100";
    public const string StringLiteralDiagnosticId = "YS101";

    private const string StringLiteralRuleTitle = "String literal should be spelled correctly";
    private const string StringLiteralRuleDescription = "String literal should be spelled correctly.";
    public const string AttributeArgumentRuleTitle = "Attribute argument should be spelled correctly";
    private const string AttributeArgumentRuleDescription = "Attribute argument should be spelled correctly.";
    private static readonly DiagnosticDescriptor StringLiteralRule = new DiagnosticDescriptor(StringLiteralDiagnosticId, StringLiteralRuleTitle, MessageFormat, ContentCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: StringLiteralRuleDescription);
    private static readonly DiagnosticDescriptor AttributeArgumentStringRule = new DiagnosticDescriptor(AttributeArgumentStringDiagnosticId, AttributeArgumentRuleTitle, MessageFormat, ContentCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: AttributeArgumentRuleDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
      StringLiteralRule,
      AttributeArgumentStringRule);

    protected override bool ConsiderEscapedCharacters => true;

    public override void Initialize(AnalysisContext context)
    {
      context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
      context.EnableConcurrentExecution();
      this.InitializeAnalyzer(context);
    }

    internal override void RegisterActions(CompilationStartAnalysisContext context, CompilationSpellcheckState state)
    {
      context.RegisterSyntaxNodeAction(nodeContext => this.AnalyzeStringLiteralToken(nodeContext, state), SyntaxKind.StringLiteralExpression);
    }

    private protected override bool CheckWord(DiagnosticDescriptor rule, string word, Location wordLocation, SyntaxNodeAnalysisContext context, IEnumerable<ILanguage> languages, CompilationSpellcheckState state)
    {
      if (!base.CheckWord(rule, word, wordLocation, context, languages, state))
      {
        ReportWord(rule, word, wordLocation, context, languages, state);
      }

      return true;
    }

    private void AnalyzeStringLiteralToken(SyntaxNodeAnalysisContext context, CompilationSpellcheckState state)
    {
      if (context.Node?.Parent is AttributeArgumentSyntax attributeArgumentSyntax)
      {
        this.AnalyzeAttributeArgument(context, attributeArgumentSyntax, state);
      }
      else
      {
        if (context.Node is LiteralExpressionSyntax literalExpressionSyntax)
        {
          var token = literalExpressionSyntax.Token;
          var text = token.ValueText;
          var nodeLocation = literalExpressionSyntax.GetLocation();
          var stringLocation = Location.Create(context.Node.SyntaxTree, TextSpan.FromBounds(nodeLocation.SourceSpan.Start + 1, nodeLocation.SourceSpan.End - 1));
          var languages = state.LanguagesByRule(StringLiteralRule.Id);
          var queuedForLanguageTool = state.QueueLanguageToolText(text, stringLocation, languages, LanguageToolTextKind.StringLiteral);
          if (!queuedForLanguageTool || state.LanguageToolAutoFallback)
          {
            this.CheckLine(StringLiteralRule, text, stringLocation, context, languages, state);
          }
        }
      }
    }

    private void AnalyzeAttributeArgument(SyntaxNodeAnalysisContext context, AttributeArgumentSyntax attributeArgumentSyntax, CompilationSpellcheckState state)
    {
      if (attributeArgumentSyntax.Parent?.Parent is AttributeSyntax attributeSyntax)
      {
        var attributeName = attributeSyntax.Name.ToFullString();
        var spellcheckSettings = state.Settings;
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
          if (context.Node is LiteralExpressionSyntax literalExpressionSyntax)
          {
            var foo = literalExpressionSyntax.Token;
            var text = foo.ValueText;
            var nodeLocation = literalExpressionSyntax.GetLocation();
            var stringLocation = Location.Create(context.Node.SyntaxTree, TextSpan.FromBounds(nodeLocation.SourceSpan.Start + 1, nodeLocation.SourceSpan.End - 1));

            var queuedForLanguageTool = state.QueueLanguageToolText(text, stringLocation, attributePropertyLanguages.Languages, LanguageToolTextKind.AttributeArgument);
            if (!queuedForLanguageTool || state.LanguageToolAutoFallback)
            {
              this.CheckLine(AttributeArgumentStringRule, text, stringLocation, context, attributePropertyLanguages.Languages, state);
            }
          }
        }
      }
    }

    private string? DetermineAttributeArgumentName(SemanticModel semanticModel, AttributeArgumentSyntax attributeArgumentSyntax)
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
      if (attributeArgumentSyntax.Parent?.Parent is AttributeSyntax attributeSyntax)
      {
        var attributeTypeInfo = semanticModel.GetTypeInfo(attributeSyntax);
        if (attributeTypeInfo.Type is INamedTypeSymbol namedTypeSymbol)
        {
          if (attributeSyntax.ArgumentList != null)
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
      }

      return null;
    }
  }
}
