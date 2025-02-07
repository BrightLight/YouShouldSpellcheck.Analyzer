﻿namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Generic;
  using System.Collections.Immutable;
  using System.Linq;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Diagnostics;

  /// <summary>
  /// This analyzer is designed to detect potential spelling mistakes in XML comments.
  /// </summary>
  [DiagnosticAnalyzer(LanguageNames.CSharp)]
  public class XmlTextSpellcheckAnalyzer : SpellcheckAnalyzerBase
  {
    public const string CommentDiagnosticId = "YS106";
    private const string CommentRuleTitle = "Comment should be spelled correctly";
    private const string CommentRuleDescription = "Comment should be spelled correctly.";
    private static readonly DiagnosticDescriptor CommentRule = new DiagnosticDescriptor(CommentDiagnosticId, CommentRuleTitle, MessageFormat, CommentCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: CommentRuleDescription);

    /// <summary>
    /// Gets the supported diagnostics for this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(CommentRule);

    protected override bool ConsiderEscapedCharacters => false;

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
      context.RegisterSyntaxNodeAction(this.AnalyzeXmlText, SyntaxKind.XmlText);
    }

    protected override bool CheckWord(DiagnosticDescriptor rule, string word, Location wordLocation, SyntaxNodeAnalysisContext context, IEnumerable<ILanguage> languages)
    {
      if (!base.CheckWord(rule, word, wordLocation, context, languages))
      {
        ReportWord(rule, word, wordLocation, context);
      }

      return true;
    }

    protected void CheckAllTokensOfType(SyntaxNodeAnalysisContext context, SyntaxNode syntaxNode, SyntaxKind syntaxKind)
    {
      foreach (var syntaxToken in syntaxNode.ChildTokens().Where(x => x.IsKind(syntaxKind)))
      {
        this.CheckToken(CommentRule, context, syntaxToken);
      }
    }

    private void AnalyzeXmlText(SyntaxNodeAnalysisContext context)
    {
      try
      {
        AnalyzerContext.InitializeSettings(context);
        if (context.Node is XmlTextSyntax xmlTextSyntax)
        {
          this.CheckAllTokensOfType(context, xmlTextSyntax, SyntaxKind.XmlTextLiteralToken);
        }
      }
      catch (Exception e)
      {
        Logger.Log(e);
        Console.WriteLine(e);
      }
    }
  }
}
