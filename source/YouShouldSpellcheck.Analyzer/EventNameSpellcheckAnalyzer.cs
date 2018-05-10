namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Immutable;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Diagnostics;

  [DiagnosticAnalyzer(LanguageNames.CSharp)]
  public class EventNameSpellcheckAnalyzer : IdentifierNameSpellcheckAnalyzer
  {
    public const string EventNameDiagnosticId = "YS109";
    private const string EventNameRuleTitle = "Name of Eventeration should be spelled correctly.";
    private const string EventNameRuleDescription = "The name of an Eventeration should be spelled correctly.";
    private static readonly DiagnosticDescriptor EventNameRule = new DiagnosticDescriptor(EventNameDiagnosticId, EventNameRuleTitle, MessageFormat, NamingCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: EventNameRuleDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(EventNameRule);

    public override void Initialize(AnalysisContext context)
    {
      context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
      context.EnableConcurrentExecution();

      // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
      // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
      context.RegisterSyntaxNodeAction(this.AnalyzeEventDeclaration, SyntaxKind.EventDeclaration);

      ////string x;
      ////context.RegisterSyntaxTreeAction(analysisContext => x = analysisContext.Tree.FilePath);
    }

    private void AnalyzeEventDeclaration(SyntaxNodeAnalysisContext context)
    {
      try
      {
        AnalyzerContext.InitializeSettings(context);
        var eventDeclarationSyntax = context.Node as EventDeclarationSyntax;
        this.CheckToken(EventNameRule, context, eventDeclarationSyntax?.Identifier);
      }
      catch (Exception e)
      {
        Logger.Log(e);
        Console.WriteLine(e);
      }
    }
  }
}