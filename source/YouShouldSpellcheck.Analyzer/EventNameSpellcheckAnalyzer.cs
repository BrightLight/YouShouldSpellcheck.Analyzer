namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Immutable;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Diagnostics;

  public sealed class EventNameSpellcheckAnalyzer : IdentifierNameSpellcheckAnalyzer
  {
    public const string EventNameDiagnosticId = "YS109";
    private const string EventNameRuleTitle = "Event name should be spelled correctly";
    private const string EventNameRuleDescription = "Event name should be spelled correctly.";
    private static readonly DiagnosticDescriptor EventNameRule = new DiagnosticDescriptor(EventNameDiagnosticId, EventNameRuleTitle, MessageFormat, NamingCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: EventNameRuleDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(EventNameRule);

    public override void Initialize(AnalysisContext context)
    {
      context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
      context.EnableConcurrentExecution();
      this.InitializeAnalyzer(context);
    }

    internal override void RegisterActions(CompilationStartAnalysisContext context, CompilationSpellcheckState state)
    {
      context.RegisterSyntaxNodeAction(nodeContext => this.AnalyzeEventDeclaration(nodeContext, state), SyntaxKind.EventDeclaration);
    }

    private void AnalyzeEventDeclaration(SyntaxNodeAnalysisContext context, CompilationSpellcheckState state)
    {
      var eventDeclarationSyntax = context.Node as EventDeclarationSyntax;
      this.CheckToken(EventNameRule, context, eventDeclarationSyntax?.Identifier, state);
    }
  }
}
