namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Immutable;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Diagnostics;

  [DiagnosticAnalyzer(LanguageNames.CSharp)]
  public class EnumMemberNameSpellcheckAnalyzer : IdentifierNameSpellcheckAnalyzer
  {
    public const string EnumMemberNameDiagnosticId = "YS108";
    private const string EnumMemberNameRuleTitle = "Name of enumeration member should be spelled correctly.";
    private const string EnumMemberNameRuleDescription = "The name of an enumeration member should be spelled correctly.";
    private static readonly DiagnosticDescriptor EnumMemberNameRule = new DiagnosticDescriptor(EnumMemberNameDiagnosticId, EnumMemberNameRuleTitle, MessageFormat, NamingCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: EnumMemberNameRuleDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(EnumMemberNameRule);

    public override void Initialize(AnalysisContext context)
    {
      context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
      context.EnableConcurrentExecution();

      // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
      // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
      context.RegisterSyntaxNodeAction(this.AnalyzeEnumMemberDeclaration, SyntaxKind.EnumMemberDeclaration);

      ////string x;
      ////context.RegisterSyntaxTreeAction(analysisContext => x = analysisContext.Tree.FilePath);
    }

    private void AnalyzeEnumMemberDeclaration(SyntaxNodeAnalysisContext context)
    {
      try
      {
        AnalyzerContext.InitializeSettings(context);
        var enumMemberDeclarationSyntax = context.Node as EnumMemberDeclarationSyntax;
        this.CheckToken(EnumMemberNameRule, context, enumMemberDeclarationSyntax?.Identifier);
      }
      catch (Exception e)
      {
        Logger.Log(e);
        Console.WriteLine(e);
      }
    }
  }
}