namespace YouShouldSpellcheck.Analyzer
{
  using System.Collections.Immutable;
  using System.Linq;
  using System.Text.RegularExpressions;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.Diagnostics;

  public abstract class SpellcheckAnalyzerBase : DiagnosticAnalyzer
  {
    public const string AttributeArgumentStringDiagnosticId = "YS100";

    // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
    // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
    public const string Title = "Spelling error";

    public const string MessageFormat = "Spelling error: {0}";

    private const string AttributeArgumentRuleDescription = "Attribute argument should be spelled correctly.";

    public const string NamingCategory = "Naming";

    public const string CommentCategory = "Comment";

    public const string ContentCategory = "Content";

    private static readonly DiagnosticDescriptor AttributeArgumentStringRule = new DiagnosticDescriptor(AttributeArgumentStringDiagnosticId, Title, MessageFormat, ContentCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: AttributeArgumentRuleDescription);

    // See http://stackoverflow.com/questions/7311734/split-sentence-into-words-but-having-trouble-with-the-punctuations-in-c-sharp
    private readonly Regex splitLineIntoWords = new Regex(@"((\b[^\s.]+\b)((?<=\.\w).)?)", RegexOptions.Compiled);

    ////private Regex splitWordsByCasing = new Regex(@"([A-Z]+|[a-z])[a-z]*", RegexOptions.Compiled);
    private readonly Regex splitWordsByCasing = new Regex(@"(\p{Lu}+|\p{Ll})\p{Ll}*", RegexOptions.Compiled);

    private readonly Regex isGuid = new Regex(@"[{(]?[0-9A-Fa-f]{8}[-]?([0-9A-Fa-f]{4}[-]?){3}[0-9A-Fa-f]{12}[)}]?", RegexOptions.Compiled);

    protected void CheckAllTokensOfType(DiagnosticDescriptor rule, SyntaxNodeAnalysisContext context, SyntaxNode syntaxNode, SyntaxKind syntaxKind)
    {
      foreach (var syntaxToken in syntaxNode.ChildTokens().Where(x => x.IsKind(syntaxKind)))
      {
        var text = syntaxToken.ValueText;
        if (string.IsNullOrWhiteSpace(text))
        {
          continue;
        }

        this.CheckLine(rule, text, syntaxToken.GetLocation(), context);
      }
    }

    protected void CheckLine(DiagnosticDescriptor rule, string line, Location location, SyntaxNodeAnalysisContext context)
    {
      if (string.IsNullOrWhiteSpace(line))
      {
        return;
      }

      foreach (var wordMatch in this.splitLineIntoWords.Matches(line).OfType<Match>())
      {
        var wordLocation = Location.Create(context.Node.SyntaxTree, Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(location.SourceSpan.Start + wordMatch.Index, location.SourceSpan.Start + wordMatch.Index + wordMatch.Length));
        this.CheckWord(rule, wordMatch.Value, wordLocation, context);
      }
    }

    protected void CheckWord(DiagnosticDescriptor rule, string word, Location wordLocation, SyntaxNodeAnalysisContext context)
    {
      // check if the whole "word" with exactly that casing is configured as a custom word (e.g. "HiFi")
      if (DictionaryManager.IsCustomWord(word))
      {
        return;
      }

      // check if the "word" actually represents a GUID which should not further be parsed
      if (this.isGuid.IsMatch(word))
      {
        return;
      }

      var wordParts = this.splitWordsByCasing.Matches(word).OfType<Match>();
      foreach (var wordPart in wordParts)
      {
        var wordPartLocation = Location.Create(context.Node.SyntaxTree, Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(wordLocation.SourceSpan.Start + wordPart.Index, wordLocation.SourceSpan.Start + wordPart.Index + wordPart.Length));
        CheckWordParts(rule, wordPart.Value, wordPartLocation, context);
      }
    }

    private static void CheckWordParts(DiagnosticDescriptor rule, string word, Location location, SyntaxNodeAnalysisContext context)
    {
      if (!string.IsNullOrWhiteSpace(word)
        && !IsWordCorrect(word, LanguagesByRule(rule.Id)))
      {
        var propertyBagForFixProvider = ImmutableDictionary.Create<string, string>();
        propertyBagForFixProvider = propertyBagForFixProvider.Add("offendingWord", word);
        var diagnostic = Diagnostic.Create(rule, location, propertyBagForFixProvider, word);
        context.ReportDiagnostic(diagnostic);
      }
    }

    private static bool IsWordCorrect(string word, string[] languages)
    {
      return languages.Any(language => DictionaryManager.IsWordCorrect(word, language));
    }

    public static string[] LanguagesByRule(string ruleId)
    {
      switch (ruleId)
      {
        case ClassNameSpellcheckAnalyzer.ClassNameDiagnosticId: return SpellcheckSettings.ClassNameLanguagses;
        case MethodNameSpellcheckAnalyzer.MethodNameDiagnosticId: return SpellcheckSettings.MethodNameLanguagses;
        case VariableNameSpellcheckAnalyzer.VariableNameDiagnosticId: return SpellcheckSettings.VariableNameLanguagses;
        case PropertyNameSpellcheckAnalyzer.PropertyNameDiagnosticId: return SpellcheckSettings.PropertyNameLanguagses;
        case AttributeArgumentStringDiagnosticId: return SpellcheckSettings.AttributeArgumentLanguages;
        case XmlTextSpellcheckAnalyzer.CommentDiagnosticId: return SpellcheckSettings.CommentLanguagses;
        case StringLiteralSpellcheckAnalyzer.StringLiteralDiagnosticId: return SpellcheckSettings.StringLiteralLanguages;
        default: return SpellcheckSettings.DefaultLanguages;
      }
    }
  }
}