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
    public const string ConfigurationDiagnosticId = ConfigurationDiagnostics.InvalidConfigurationDiagnosticId;

    private const string StringLiteralRuleTitle = "String literal should be spelled correctly";
    private const string StringLiteralRuleDescription = "String literal should be spelled correctly.";
    public const string AttributeArgumentRuleTitle = "Attribute argument should be spelled correctly";
    private const string AttributeArgumentRuleDescription = "Attribute argument should be spelled correctly.";
    private static readonly DiagnosticDescriptor StringLiteralRule = new DiagnosticDescriptor(StringLiteralDiagnosticId, StringLiteralRuleTitle, MessageFormat, ContentCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: StringLiteralRuleDescription);
    private static readonly DiagnosticDescriptor AttributeArgumentStringRule = new DiagnosticDescriptor(AttributeArgumentStringDiagnosticId, AttributeArgumentRuleTitle, MessageFormat, ContentCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: AttributeArgumentRuleDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
      StringLiteralRule,
      AttributeArgumentStringRule,
      ConfigurationDiagnostics.InvalidConfiguration);

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
      if (!state.ConfigurationErrors.IsDefaultOrEmpty)
      {
        context.RegisterCompilationEndAction(endContext =>
        {
          foreach (var error in state.ConfigurationErrors)
          {
            endContext.ReportDiagnostic(Diagnostic.Create(ConfigurationDiagnostics.InvalidConfiguration, Location.None, error));
          }
        });
      }
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
          var sourceMap = StringLiteralSourceMap.Create(literalExpressionSyntax.Token);
          var text = sourceMap.Token.ValueText;
          var stringLocation = sourceMap.ContentLocation;
          var languages = state.LanguagesByRule(StringLiteralRule.Id);
          var queuedForLanguageTool = state.QueueLanguageToolText(text, stringLocation, languages, LanguageToolTextKind.StringLiteral, sourceMap.SourcePositions);
          if (!queuedForLanguageTool || state.LanguageToolAutoFallback)
          {
            this.CheckLine(StringLiteralRule, text, stringLocation, context, languages, state, sourceMap.GetSourcePosition);
          }
        }
      }
    }

    private void AnalyzeAttributeArgument(SyntaxNodeAnalysisContext context, AttributeArgumentSyntax attributeArgumentSyntax, CompilationSpellcheckState state)
    {
      if (context.Node is not LiteralExpressionSyntax literalExpressionSyntax
        || !TryDetermineAttributeArgumentTarget(
          context.SemanticModel,
          attributeArgumentSyntax,
          context.CancellationToken,
          out var attributeType,
          out var memberName,
          out var kind))
      {
        return;
      }

      var attributeArgumentRule = state.FindAttributeArgumentRule(attributeType, memberName, kind);
      if (attributeArgumentRule == null)
      {
        return;
      }

      var sourceMap = StringLiteralSourceMap.Create(literalExpressionSyntax.Token);
      var text = sourceMap.Token.ValueText;
      var stringLocation = sourceMap.ContentLocation;
      var queuedForLanguageTool = state.QueueLanguageToolText(text, stringLocation, attributeArgumentRule.Languages, LanguageToolTextKind.AttributeArgument, sourceMap.SourcePositions);
      if (!queuedForLanguageTool || state.LanguageToolAutoFallback)
      {
        this.CheckLine(AttributeArgumentStringRule, text, stringLocation, context, attributeArgumentRule.Languages, state, sourceMap.GetSourcePosition);
      }
    }

    private static bool TryDetermineAttributeArgumentTarget(
      SemanticModel semanticModel,
      AttributeArgumentSyntax attributeArgumentSyntax,
      System.Threading.CancellationToken cancellationToken,
      out INamedTypeSymbol attributeType,
      out string memberName,
      out AttributeArgumentKind kind)
    {
      attributeType = null!;
      memberName = string.Empty;
      kind = AttributeArgumentKind.Any;
      if (attributeArgumentSyntax.Parent?.Parent is not AttributeSyntax attributeSyntax
        || semanticModel.GetSymbolInfo(attributeSyntax, cancellationToken).Symbol is not IMethodSymbol constructor)
      {
        return false;
      }

      attributeType = constructor.ContainingType;
      if (attributeArgumentSyntax.NameEquals != null)
      {
        memberName = attributeArgumentSyntax.NameEquals.Name.Identifier.ValueText;
        kind = AttributeArgumentKind.NamedMember;
        return true;
      }

      if (attributeArgumentSyntax.NameColon != null)
      {
        memberName = attributeArgumentSyntax.NameColon.Name.Identifier.ValueText;
        kind = AttributeArgumentKind.ConstructorParameter;
        return true;
      }

      var constructorArguments = attributeSyntax.ArgumentList?.Arguments
        .Where(argument => argument.NameEquals == null)
        .ToList();
      var argumentIndex = constructorArguments?.IndexOf(attributeArgumentSyntax) ?? -1;
      if (argumentIndex < 0 || constructor.Parameters.Length == 0)
      {
        return false;
      }

      var parameterIndex = argumentIndex < constructor.Parameters.Length
        ? argumentIndex
        : constructor.Parameters.Length - 1;
      var parameter = constructor.Parameters[parameterIndex];
      if (argumentIndex >= constructor.Parameters.Length && !parameter.IsParams)
      {
        return false;
      }

      memberName = parameter.Name;
      kind = AttributeArgumentKind.ConstructorParameter;
      return true;
    }
  }
}
