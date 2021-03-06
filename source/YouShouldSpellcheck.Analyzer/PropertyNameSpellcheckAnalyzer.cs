﻿
namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Immutable;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Diagnostics;

  [DiagnosticAnalyzer(LanguageNames.CSharp)]
  public class PropertyNameSpellcheckAnalyzer : IdentifierNameSpellcheckAnalyzer
  {
    public const string PropertyNameDiagnosticId = "YS105";
    private const string PropertyNameRuleTitle = "Property name should be spelled correctly.";
    private const string PropertyNameRuleDescription = "Property name should be spelled correctly.";
    private static readonly DiagnosticDescriptor PropertyNameRule = new DiagnosticDescriptor(PropertyNameDiagnosticId, PropertyNameRuleTitle, MessageFormat, NamingCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: PropertyNameRuleDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(PropertyNameRule);

    public override void Initialize(AnalysisContext context)
    {
      context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
      context.EnableConcurrentExecution();

      // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
      // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
      ////context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
      context.RegisterSyntaxNodeAction(this.AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
    }

    private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
    {
      try
      {
        AnalyzerContext.InitializeSettings(context);
        var propertyDeclarationSyntax = context.Node as PropertyDeclarationSyntax;
        this.CheckToken(PropertyNameRule, context, propertyDeclarationSyntax?.Identifier);
      }
      catch (Exception e)
      {
        Logger.Log(e);
        Console.WriteLine(e);
      }
    }

  }
}
