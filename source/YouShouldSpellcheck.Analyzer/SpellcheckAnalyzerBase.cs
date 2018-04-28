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

    private readonly Regex isGuid = new Regex(@"[{(]?[0-9A-Fa-f]{8}[-]?([0-9A-Fa-f]{4}[-]?){3}[0-9A-Fa-f]{12}[)}]?", RegexOptions.Compiled);

    protected void CheckAllTokensOfType(DiagnosticDescriptor rule, SyntaxNodeAnalysisContext context, SyntaxNode syntaxNode, SyntaxKind syntaxKind)
    {
      foreach (var syntaxToken in syntaxNode.ChildTokens().Where(x => x.IsKind(syntaxKind)))
      {
        this.CheckToken(rule, context, syntaxToken);
      }
    }

    protected void CheckToken(DiagnosticDescriptor rule, SyntaxNodeAnalysisContext context, SyntaxToken? syntaxToken)
    {
      if (syntaxToken.HasValue)
      {
        this.CheckToken(rule, context, syntaxToken.Value);
      }
    }

    protected void CheckToken(DiagnosticDescriptor rule, SyntaxNodeAnalysisContext context, SyntaxToken syntaxToken)
    {
      var text = syntaxToken.ValueText;
      if (string.IsNullOrWhiteSpace(text))
      {
        return;
      }

      this.CheckText(rule, text, syntaxToken.GetLocation(), context);
    }

    protected virtual void CheckText(DiagnosticDescriptor rule, string text, Location location, SyntaxNodeAnalysisContext context)
    {
      this.CheckLine(rule, text, location, context);
    }

    protected void CheckLine(DiagnosticDescriptor rule, string line, Location location, SyntaxNodeAnalysisContext context)
    {
      if (string.IsNullOrWhiteSpace(line))
      {
        return;
      }

      Logger.Log($"{this.GetType().Name} - CheckLine: [{line}]");
      foreach (var wordMatch in this.splitLineIntoWords.Matches(line).OfType<Match>())
      {
        var wordLocation = Location.Create(context.Node.SyntaxTree, Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(location.SourceSpan.Start + wordMatch.Index, location.SourceSpan.Start + wordMatch.Index + wordMatch.Length));
        this.CheckWord(rule, wordMatch.Value, wordLocation, context);
      }
    }

    protected virtual bool CheckWord(DiagnosticDescriptor rule, string word, Location wordLocation, SyntaxNodeAnalysisContext context)
    {
      // check if the "word" actually represents a GUID which should not further be parsed
      if (this.isGuid.IsMatch(word))
      {
        return true;
      }

      // check if the word is correct
      if (IsWordCorrect(word, LanguagesByRule(rule.Id)))
      {
        return true;
      }

      return false;
    }

    protected static bool IsWordCorrect(string word, string[] languages)
    {
      return string.IsNullOrWhiteSpace(word)
        || languages == null
        || languages.Any(language => DictionaryManager.IsWordCorrect(word, language));
    }

    protected static void ReportWord(DiagnosticDescriptor rule, string word, Location location, SyntaxNodeAnalysisContext context)
    {
      var propertyBagForFixProvider = ImmutableDictionary.Create<string, string>();
      propertyBagForFixProvider = propertyBagForFixProvider.Add("offendingWord", word);
      var diagnostic = Diagnostic.Create(rule, location, propertyBagForFixProvider, word);
      context.ReportDiagnostic(diagnostic);
    }

    public static string[] LanguagesByRule(string ruleId)
    {
      switch (ruleId)
      {
        case ClassNameSpellcheckAnalyzer.ClassNameDiagnosticId:
          return AnalyzerContext.SpellcheckSettings.ClassNameLanguages;
        case MethodNameSpellcheckAnalyzer.MethodNameDiagnosticId:
          return AnalyzerContext.SpellcheckSettings.MethodNameLanguages;
        case VariableNameSpellcheckAnalyzer.VariableNameDiagnosticId:
          return AnalyzerContext.SpellcheckSettings.VariableNameLanguages;
        case PropertyNameSpellcheckAnalyzer.PropertyNameDiagnosticId:
          return AnalyzerContext.SpellcheckSettings.PropertyNameLanguages;
        case AttributeArgumentStringDiagnosticId:
          return AnalyzerContext.SpellcheckSettings.AttributeArgumentLanguages;
        case XmlTextSpellcheckAnalyzer.CommentDiagnosticId:
          return AnalyzerContext.SpellcheckSettings.CommentLanguages;
        case StringLiteralSpellcheckAnalyzer.StringLiteralDiagnosticId:
          return AnalyzerContext.SpellcheckSettings.StringLiteralLanguages;
        case EnumNameSpellcheckAnalyzer.EnumNameDiagnosticId:
          return AnalyzerContext.SpellcheckSettings.EnumNameLanguages;
        case EnumMemberNameSpellcheckAnalyzer.EnumMemberNameDiagnosticId:
          return AnalyzerContext.SpellcheckSettings.EnumMemberNameLanguages;
        case EventNameSpellcheckAnalyzer.EventNameDiagnosticId:
          return AnalyzerContext.SpellcheckSettings.EventNameLanguages;
        default:
          return AnalyzerContext.SpellcheckSettings.DefaultLanguages;
      }
    }
  }
}