namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Immutable;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Diagnostics;

  [DiagnosticAnalyzer(LanguageNames.CSharp)]
  public class ClassNameSpellcheckAnalyzer : IdentifierNameSpellcheckAnalyzer
  {
    public const string ClassNameDiagnosticId = "YS103";
    private const string ClassNameRuleTitle = "Class name should be spelled correctly";
    private const string ClassNameRuleDescription = "Class name should be spelled correctly.";
    private static readonly DiagnosticDescriptor ClassNameRule = new DiagnosticDescriptor(ClassNameDiagnosticId, ClassNameRuleTitle, MessageFormat, NamingCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: ClassNameRuleDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ClassNameRule);

    public override void Initialize(AnalysisContext context)
    {
      context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
      context.EnableConcurrentExecution();

      // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
      // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
      context.RegisterSyntaxNodeAction(this.AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);

      ////string x;
      ////context.RegisterSyntaxTreeAction(analysisContext => x = analysisContext.Tree.FilePath);
    }

    private void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
      try
      {
        AnalyzerContext.InitializeSettings(context);
        var classDeclarationSyntax = context.Node as ClassDeclarationSyntax;
        this.CheckToken(ClassNameRule, context, classDeclarationSyntax?.Identifier);
      }
      catch (Exception e)
      {
        Logger.Log(e);
        Console.WriteLine(e);
      }
    }
  }
}

