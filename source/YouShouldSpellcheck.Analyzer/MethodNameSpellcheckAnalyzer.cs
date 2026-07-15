namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Immutable;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Diagnostics;

  /// <summary>
  /// This analyzer is designed to detect potential spelling mistakes in method names.
  /// </summary>
  public sealed class MethodNameSpellcheckAnalyzer : IdentifierNameSpellcheckAnalyzer
  {
    public const string MethodNameDiagnosticId = "YS104";
    private const string MethodNameRuleTitle = "Method name should be spelled correctly";
    private const string MethodNameRuleDescription = "Method name should be spelled correctly.";
    private static readonly DiagnosticDescriptor MethodNameRule = new DiagnosticDescriptor(MethodNameDiagnosticId, MethodNameRuleTitle, MessageFormat, NamingCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: MethodNameRuleDescription);

    /// <summary>
    /// Gets the supported diagnostics for this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(MethodNameRule);

    /// <summary>
    /// Initializes the analyzer by registering the actions that it will perform.
    /// </summary>
    /// <param name="context"></param>
    public override void Initialize(AnalysisContext context)
    {
      context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
      context.EnableConcurrentExecution();
      this.InitializeAnalyzer(context);
    }

    internal override void RegisterActions(CompilationStartAnalysisContext context, CompilationSpellcheckState state)
    {
      context.RegisterSyntaxNodeAction(nodeContext => this.AnalyzeMethodDeclaration(nodeContext, state), SyntaxKind.MethodDeclaration);
    }

    private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context, CompilationSpellcheckState state)
    {
      var methodDeclarationSyntax = context.Node as MethodDeclarationSyntax;
      this.CheckToken(MethodNameRule, context, methodDeclarationSyntax?.Identifier, state);
    }
  }
}
