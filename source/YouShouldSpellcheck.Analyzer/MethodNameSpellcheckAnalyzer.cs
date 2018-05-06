﻿namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Immutable;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Diagnostics;

  [DiagnosticAnalyzer(LanguageNames.CSharp)]
  public class MethodNameSpellcheckAnalyzer : IdentifierNameSpellcheckAnalyzer
  {
    public const string MethodNameDiagnosticId = "YS104";
    private const string MethodNameRuleTitle = "Method name should be spelled correctly.";
    private const string MethodNameRuleDescription = "Method name should be spelled correctly.";
    private static readonly DiagnosticDescriptor MethodNameRule = new DiagnosticDescriptor(MethodNameDiagnosticId, MethodNameRuleTitle, MessageFormat, NamingCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: MethodNameRuleDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(MethodNameRule);

    public override void Initialize(AnalysisContext context)
    {
      context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
      context.EnableConcurrentExecution();

      // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
      // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
      context.RegisterSyntaxNodeAction(this.AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
    }

    private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
      try
      {
        AnalyzerContext.InitializeSettings(context);
        var methodDeclarationSyntax = context.Node as MethodDeclarationSyntax;
        this.CheckToken(MethodNameRule, context, methodDeclarationSyntax?.Identifier);
      }
      catch (Exception e)
      {
        Logger.Log(e.ToString());
        Console.WriteLine(e);
      }
    }
  }
}