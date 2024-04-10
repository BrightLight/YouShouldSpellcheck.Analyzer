namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Immutable;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Diagnostics;

  /// <summary>
  /// This analyzer is designed to detect potential spelling mistakes in variable names.
  /// </summary>
  [DiagnosticAnalyzer(LanguageNames.CSharp)]
  public class VariableNameSpellcheckAnalyzer : IdentifierNameSpellcheckAnalyzer
  {
    public const string VariableNameDiagnosticId = "YS102";

    private const string VariableNameRuleTitle = "Variable name should be spelled correctly";

    private const string VariableNameRuleDescription = "Variable name should be spelled correctly.";

    private static readonly DiagnosticDescriptor VariableNameRule = new DiagnosticDescriptor(VariableNameDiagnosticId, VariableNameRuleTitle, MessageFormat, NamingCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: VariableNameRuleDescription);

    /// <summary>
    /// Gets the supported diagnostics for this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(VariableNameRule);

    /// <summary>
    /// Initializes the analyzer by registering the actions that it will perform.
    /// </summary>
    /// <param name="context">The context in which the analyzer is being run.</param>
    public override void Initialize(AnalysisContext context)
    {
      context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
      context.EnableConcurrentExecution();

      // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
      // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
      context.RegisterSyntaxNodeAction(this.AnalyzeVariableDeclarator, SyntaxKind.VariableDeclarator);
    }

    private void AnalyzeVariableDeclarator(SyntaxNodeAnalysisContext context)
    {
      try
      {
        AnalyzerContext.InitializeSettings(context);
        var variableDeclaratorSyntax = context.Node as VariableDeclaratorSyntax;
        this.CheckToken(VariableNameRule, context, variableDeclaratorSyntax?.Identifier);
      }
      catch (Exception e)
      {
        Logger.Log(e);
        Console.WriteLine(e);
      }
    }
  }
}
