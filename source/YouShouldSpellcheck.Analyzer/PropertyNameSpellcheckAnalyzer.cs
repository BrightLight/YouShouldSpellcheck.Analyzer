
namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Immutable;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Diagnostics;

  /// <summary>
  /// This analyzer is designed to detect potential spelling mistakes in property names.
  /// </summary>
  public sealed class PropertyNameSpellcheckAnalyzer : IdentifierNameSpellcheckAnalyzer
  {
    public const string PropertyNameDiagnosticId = "YS105";
    private const string PropertyNameRuleTitle = "Property name should be spelled correctly";
    private const string PropertyNameRuleDescription = "Property name should be spelled correctly.";
    private static readonly DiagnosticDescriptor PropertyNameRule = new DiagnosticDescriptor(PropertyNameDiagnosticId, PropertyNameRuleTitle, MessageFormat, NamingCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: PropertyNameRuleDescription);

    /// <summary>
    /// Gets the supported diagnostics for this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(PropertyNameRule);

    /// <summary>
    /// Initializes the analyzer by registering the actions that it will perform.
    /// </summary>
    /// <param name="context">The context in which the analyzer is being run.</param>
    public override void Initialize(AnalysisContext context)
    {
      context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
      context.EnableConcurrentExecution();
      this.InitializeAnalyzer(context);
    }

    internal override void RegisterActions(CompilationStartAnalysisContext context, CompilationSpellcheckState state)
    {
      context.RegisterSyntaxNodeAction(nodeContext => this.AnalyzePropertyDeclaration(nodeContext, state), SyntaxKind.PropertyDeclaration);
    }

    private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context, CompilationSpellcheckState state)
    {
      var propertyDeclarationSyntax = context.Node as PropertyDeclarationSyntax;
      this.CheckToken(PropertyNameRule, context, propertyDeclarationSyntax?.Identifier, state);
    }

  }
}
