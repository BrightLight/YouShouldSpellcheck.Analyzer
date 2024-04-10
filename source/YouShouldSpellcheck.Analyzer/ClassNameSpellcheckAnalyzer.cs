namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Immutable;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Diagnostics;

  /// <summary>
  /// This analyzer is designed to detect potential spelling mistakes in class names.
  /// </summary>
  [DiagnosticAnalyzer(LanguageNames.CSharp)]
  public class ClassNameSpellcheckAnalyzer : IdentifierNameSpellcheckAnalyzer
  {
    public const string ClassNameDiagnosticId = "YS103";
    private const string ClassNameRuleTitle = "Class name should be spelled correctly";
    private const string ClassNameRuleDescription = "Class name should be spelled correctly.";
    private static readonly DiagnosticDescriptor ClassNameRule = new DiagnosticDescriptor(ClassNameDiagnosticId, ClassNameRuleTitle, MessageFormat, NamingCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: ClassNameRuleDescription);

    /// <summary>
    /// Gets the supported diagnostics for this analyzer. 
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ClassNameRule);

    /// <summary>
    /// Initializes the analyzer by registering the actions that it will perform.
    /// </summary>
    /// <param name="context">The context in which the analyzer is being run.</param>
    public override void Initialize(AnalysisContext context)
    {
      context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
      context.EnableConcurrentExecution();

      // Register an action to analyze class declarations
      // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
      // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
      context.RegisterSyntaxNodeAction(this.AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);

      ////string x;
      ////context.RegisterSyntaxTreeAction(analysisContext => x = analysisContext.Tree.FilePath);
    }

    /// <summary>
    /// Analyzes class declarations to check for spelling mistakes in the class name.
    /// </summary>
    /// <param name="context">The context in which the syntax node is being analyzed.</param>
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

