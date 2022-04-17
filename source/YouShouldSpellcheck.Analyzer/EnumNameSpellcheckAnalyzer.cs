namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Immutable;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Diagnostics;

  [DiagnosticAnalyzer(LanguageNames.CSharp)]
  public class EnumNameSpellcheckAnalyzer : IdentifierNameSpellcheckAnalyzer
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

      // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
      // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
      context.RegisterSyntaxNodeAction(this.AnalyzeEnumDeclaration, SyntaxKind.EnumDeclaration);

      ////string x;
      ////context.RegisterSyntaxTreeAction(analysisContext => x = analysisContext.Tree.FilePath);
    }

    private void AnalyzeEnumDeclaration(SyntaxNodeAnalysisContext context)
    {
      try
      {
        AnalyzerContext.InitializeSettings(context);
        var enumDeclarationSyntax = context.Node as EnumDeclarationSyntax;
        this.CheckToken(EnumNameRule, context, enumDeclarationSyntax?.Identifier);
      }
      catch (Exception e)
      {
        Logger.Log(e);
        Console.WriteLine(e);
      }
    }
  }
}