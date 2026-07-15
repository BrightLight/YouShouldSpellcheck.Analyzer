namespace YouShouldSpellcheck.Analyzer
{
  using System.Collections.Generic;
  using System.Linq;
  using System.Text.RegularExpressions;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.Diagnostics;

  public abstract class IdentifierNameSpellcheckAnalyzer :  SpellcheckAnalyzerBase
  {
    ////private Regex splitWordsByCasing = new Regex(@"([A-Z]+|[a-z])[a-z]*", RegexOptions.Compiled);
    private readonly Regex splitWordsByCasing = new Regex(@"(\p{Lu}+|\p{Ll})\p{Ll}*", RegexOptions.Compiled);

    protected override bool ConsiderEscapedCharacters => false;

    private protected override void CheckText(DiagnosticDescriptor rule, string text, Location location, SyntaxNodeAnalysisContext context, IEnumerable<ILanguage> languages, CompilationSpellcheckState state)
    {
      this.CheckWord(rule, text, location, context, languages, state);
    }

    private protected override bool CheckWord(DiagnosticDescriptor rule, string word, Location wordLocation, SyntaxNodeAnalysisContext context, IEnumerable<ILanguage> languages, CompilationSpellcheckState state)
    {
      if (base.CheckWord(rule, word, wordLocation, context, languages, state))
      {
        return true;
      }

      this.CheckWordParts(rule, word, wordLocation, context, state);

      return true;
    }

    private void CheckWordParts(DiagnosticDescriptor rule, string word, Location location, SyntaxNodeAnalysisContext context, CompilationSpellcheckState state)
    {
      var wordParts = this.splitWordsByCasing.Matches(word).OfType<Match>();
      foreach (var wordPart in wordParts)
      {
        var wordPartLocation = Location.Create(context.Node.SyntaxTree, Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(location.SourceSpan.Start + wordPart.Index, location.SourceSpan.Start + wordPart.Index + wordPart.Length));
        if (!base.CheckWord(rule, wordPart.Value, wordPartLocation, context, state.LanguagesByRule(rule.Id), state))
        {
          ReportWord(rule, wordPart.Value, wordPartLocation, context, state.LanguagesByRule(rule.Id), state);
        }
      }
    }

    private protected void CheckToken(DiagnosticDescriptor rule, SyntaxNodeAnalysisContext context, SyntaxToken? syntaxToken, CompilationSpellcheckState state)
    {
      if (syntaxToken.HasValue)
      {
        base.CheckToken(rule, context, syntaxToken.Value, state);
      }
    }
  }
}
