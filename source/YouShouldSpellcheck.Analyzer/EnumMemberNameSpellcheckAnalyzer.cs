namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Immutable;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Diagnostics;

  public sealed class EnumMemberNameSpellcheckAnalyzer : IdentifierNameSpellcheckAnalyzer
  {
    public const string EnumMemberNameDiagnosticId = "YS108";
    private const string EnumMemberNameRuleTitle = "Enumeration member name should be spelled correctly";
    private const string EnumMemberNameRuleDescription = "Enumeration member name should be spelled correctly.";
    private static readonly DiagnosticDescriptor EnumMemberNameRule = new DiagnosticDescriptor(EnumMemberNameDiagnosticId, EnumMemberNameRuleTitle, MessageFormat, NamingCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: EnumMemberNameRuleDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(EnumMemberNameRule);

    public override void Initialize(AnalysisContext context)
    {
      context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
      context.EnableConcurrentExecution();
      this.InitializeAnalyzer(context);
    }

    internal override void RegisterActions(CompilationStartAnalysisContext context, CompilationSpellcheckState state)
    {
      context.RegisterSyntaxNodeAction(nodeContext => this.AnalyzeEnumMemberDeclaration(nodeContext, state), SyntaxKind.EnumMemberDeclaration);
    }

    private void AnalyzeEnumMemberDeclaration(SyntaxNodeAnalysisContext context, CompilationSpellcheckState state)
    {
      var enumMemberDeclarationSyntax = context.Node as EnumMemberDeclarationSyntax;
      this.CheckToken(EnumMemberNameRule, context, enumMemberDeclarationSyntax?.Identifier, state);
    }
  }
}
