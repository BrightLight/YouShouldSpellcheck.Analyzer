namespace YouShouldSpellcheck.Analyzer
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
  public sealed class XmlTextSpellcheckAnalyzer : SpellcheckAnalyzerBase
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
      this.InitializeAnalyzer(context);
    }

    internal override void RegisterActions(CompilationStartAnalysisContext context, CompilationSpellcheckState state)
    {
      context.RegisterSyntaxNodeAction(nodeContext => this.AnalyzeXmlText(nodeContext, state), SyntaxKind.XmlText);
    }

    private protected override bool CheckWord(DiagnosticDescriptor rule, string word, Location wordLocation, SyntaxNodeAnalysisContext context, IEnumerable<ILanguage> languages, CompilationSpellcheckState state)
    {
      if (!base.CheckWord(rule, word, wordLocation, context, languages, state))
      {
        ReportWord(rule, word, wordLocation, context, languages, state);
      }

      return true;
    }

    private void CheckAllTokensOfType(SyntaxNodeAnalysisContext context, SyntaxNode syntaxNode, SyntaxKind syntaxKind, CompilationSpellcheckState state)
    {
      foreach (var syntaxToken in syntaxNode.ChildTokens().Where(x => x.IsKind(syntaxKind)))
      {
        var languages = state.LanguagesByRule(CommentRule.Id);
        if (!state.QueueLanguageToolText(syntaxToken.ValueText, syntaxToken.GetLocation(), languages))
        {
          this.CheckToken(CommentRule, context, syntaxToken, state);
        }
      }
    }

    private void AnalyzeXmlText(SyntaxNodeAnalysisContext context, CompilationSpellcheckState state)
    {
      if (context.Node is XmlTextSyntax xmlTextSyntax)
      {
        this.CheckAllTokensOfType(context, xmlTextSyntax, SyntaxKind.XmlTextLiteralToken, state);
      }
    }
  }
}
