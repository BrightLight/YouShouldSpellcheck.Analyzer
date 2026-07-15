namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Immutable;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Diagnostics;

  public sealed class EnumNameSpellcheckAnalyzer : IdentifierNameSpellcheckAnalyzer
  {
    public const string EnumNameDiagnosticId = "YS107";
    private const string EnumNameRuleTitle = "Enumeration name should be spelled correctly";
    private const string EnumNameRuleDescription = "Enumeration name should be spelled correctly.";
    private static readonly DiagnosticDescriptor EnumNameRule = new DiagnosticDescriptor(EnumNameDiagnosticId, EnumNameRuleTitle, MessageFormat, NamingCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: EnumNameRuleDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(EnumNameRule);

    public override void Initialize(AnalysisContext context)
    {
      context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
      context.EnableConcurrentExecution();
      this.InitializeAnalyzer(context);
    }

    internal override void RegisterActions(CompilationStartAnalysisContext context, CompilationSpellcheckState state)
    {
      context.RegisterSyntaxNodeAction(nodeContext => this.AnalyzeEnumDeclaration(nodeContext, state), SyntaxKind.EnumDeclaration);
    }

    private void AnalyzeEnumDeclaration(SyntaxNodeAnalysisContext context, CompilationSpellcheckState state)
    {
      var enumDeclarationSyntax = context.Node as EnumDeclarationSyntax;
      this.CheckToken(EnumNameRule, context, enumDeclarationSyntax?.Identifier, state);
    }
  }
}
