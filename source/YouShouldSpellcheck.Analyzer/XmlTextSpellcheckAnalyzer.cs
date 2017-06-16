namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Immutable;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;
  using Microsoft.CodeAnalysis.Diagnostics;

  [DiagnosticAnalyzer(LanguageNames.CSharp)]
  public class XmlTextSpellcheckAnalyzer : SpellcheckAnalyzerBase
  {
    public const string CommentDiagnosticId = "YS106";
    private const string CommentRuleDescription = "Comment should be spelled correctly.";
    private static readonly DiagnosticDescriptor CommentRule = new DiagnosticDescriptor(CommentDiagnosticId, Title, MessageFormat, CommentCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: CommentRuleDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(CommentRule);

    public override void Initialize(AnalysisContext context)
    {
      context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

      // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
      // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
      context.RegisterSyntaxNodeAction(this.AnalyzeXmlText, SyntaxKind.XmlText);
    }

    private void AnalyzeXmlText(SyntaxNodeAnalysisContext context)
    {
      try
      {
        var xmlTextSyntax = context.Node as XmlTextSyntax;
        if (xmlTextSyntax != null)
        {
          this.CheckAllTokensOfType(CommentRule, context, xmlTextSyntax, SyntaxKind.XmlTextLiteralToken);
        }
      }
      catch (Exception e)
      {
        Logger.Log(e.ToString());
        Console.WriteLine(e);
      }
    }
  }
}
