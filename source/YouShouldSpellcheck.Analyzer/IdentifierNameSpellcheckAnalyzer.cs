namespace YouShouldSpellcheck.Analyzer
{
  using System.Linq;
  using System.Text.RegularExpressions;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.Diagnostics;

  public abstract class IdentifierNameSpellcheckAnalyzer :  SpellcheckAnalyzerBase
  {
    ////private Regex splitWordsByCasing = new Regex(@"([A-Z]+|[a-z])[a-z]*", RegexOptions.Compiled);
    private readonly Regex splitWordsByCasing = new Regex(@"(\p{Lu}+|\p{Ll})\p{Ll}*", RegexOptions.Compiled);

    protected override void CheckText(DiagnosticDescriptor rule, string text, Location location, SyntaxNodeAnalysisContext context)
    {
      this.CheckWord(rule, text, location, context);
    }

    protected override bool CheckWord(DiagnosticDescriptor rule, string word, Location wordLocation, SyntaxNodeAnalysisContext context)
    {
      if (base.CheckWord(rule, word, wordLocation, context))
      {
        return true;
      }

      this.CheckWordParts(rule, word, wordLocation, context);

      return true;
    }

    protected void CheckWordParts(DiagnosticDescriptor rule, string word, Location location, SyntaxNodeAnalysisContext context)
    {
      var wordParts = this.splitWordsByCasing.Matches(word).OfType<Match>();
      foreach (var wordPart in wordParts)
      {
        var wordPartLocation = Location.Create(context.Node.SyntaxTree, Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(location.SourceSpan.Start + wordPart.Index, location.SourceSpan.Start + wordPart.Index + wordPart.Length));
        if (!base.CheckWord(rule, wordPart.Value, wordPartLocation,context))
        {
          ReportWord(rule, wordPart.Value, wordPartLocation, context);
        }
      }
    }
  }
}