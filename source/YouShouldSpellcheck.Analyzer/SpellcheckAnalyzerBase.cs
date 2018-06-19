namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Generic;
  using System.Collections.Immutable;
  using System.Linq;
  using System.Text;
  using System.Text.RegularExpressions;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.Diagnostics;
  using Microsoft.CodeAnalysis.Text;
  using YouShouldSpellcheck.Analyzer.LanguageTool;
  using Match = System.Text.RegularExpressions.Match;

  public abstract class SpellcheckAnalyzerBase : DiagnosticAnalyzer
  {
    // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
    // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
    public const string MessageFormat = "{0}";

    public const string NamingCategory = "Naming";

    public const string CommentCategory = "Comment";

    public const string ContentCategory = "Content";

    public const string LanguageToolCasingDiagnosticId = "YS201";
    public const string LanguageToolColloquialismsDiagnosticId = "YS202";
    public const string LanguageToolCompoundingDiagnosticId = "YS203";
    public const string LanguageToolConfusedWordsDiagnosticId = "YS204";
    public const string LanguageToolFalseFriendsDiagnosticId = "YS205";
    public const string LanguageToolGenderNeutralityDiagnosticId = "YS206";
    public const string LanguageToolGrammarDiagnosticId = "YS207";
    public const string LanguageToolMiscDiagnosticId = "YS208";
    public const string LanguageToolPunctuationDiagnosticId = "YS209";
    public const string LanguageToolRedundancyDiagnosticId = "YS210";
    public const string LanguageToolRegionalismsDiagnosticId = "YS211";
    public const string LanguageToolRepetitionsDiagnosticId = "YS212";
    public const string LanguageToolSemanticsDiagnosticId = "YS213";
    public const string LanguageToolStyleDiagnosticId = "YS214";
    public const string LanguageToolTypographyDiagnosticId = "YS215";
    public const string LanguageToolTyposDiagnosticId = "YS216";
    public const string LanguageToolWikipediaDiagnosticId = "YS217";

    private static bool languageToolIsOffline;

    // See http://stackoverflow.com/questions/7311734/split-sentence-into-words-but-having-trouble-with-the-punctuations-in-c-sharp
    // private readonly Regex splitLineIntoWords = new Regex(@"((\b[^\s.]+\b)((?<=\.\w).)?)", RegexOptions.Compiled); // original with additional "." (for reasons I no longer no) 
    private readonly Regex splitLineIntoWords = new Regex(@"((\b[^\s\/]+\b)((?<=\.\w).)?)", RegexOptions.Compiled); // version from StackOverflow modified to split forwared dash as well (e.g. "sender/receiver").

    private readonly Regex isGuid = new Regex(@"[{(]?[0-9A-Fa-f]{8}[-]?([0-9A-Fa-f]{4}[-]?){3}[0-9A-Fa-f]{12}[)}]?", RegexOptions.Compiled);

    // language tool categories (as found on https://languagetool.org/development/api/org/languagetool/rules/Categories.html)
    protected static readonly DiagnosticDescriptor LanguageToolCasingRule = new DiagnosticDescriptor(LanguageToolCasingDiagnosticId, "LanguageTool: Casing", MessageFormat, "LanguageTool:Casing", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "Rules about detecting uppercase words where lowercase is required and vice versa.");
    protected static readonly DiagnosticDescriptor LanguageToolColloquialismsRule = new DiagnosticDescriptor(LanguageToolColloquialismsDiagnosticId, "LanguageTool: Colloquialisms", MessageFormat, "LanguageTool:Colloquialisms", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "Colloquial style.");
    protected static readonly DiagnosticDescriptor LanguageToolCompoundingRule = new DiagnosticDescriptor(LanguageToolCompoundingDiagnosticId, "LanguageTool: Compounding", MessageFormat, "LanguageTool:Compounding", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "Rules about spelling terms as one word or as as separate words.");
    protected static readonly DiagnosticDescriptor LanguageToolConfusedWordsRule = new DiagnosticDescriptor(LanguageToolConfusedWordsDiagnosticId, "LanguageTool: Confused words", MessageFormat, "LanguageTool:ConfusedWords", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "Words that are easily confused, like 'there' and 'their' in English.");
    protected static readonly DiagnosticDescriptor LanguageToolFalseFriendsRule = new DiagnosticDescriptor(LanguageToolFalseFriendsDiagnosticId, "LanguageTool: False friends", MessageFormat, "LanguageTool:FalseFriends", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "False friends: words easily confused by language learners because a similar word exists in their native language.");
    protected static readonly DiagnosticDescriptor LanguageToolGenderNeutralityRule = new DiagnosticDescriptor(LanguageToolGenderNeutralityDiagnosticId, "LanguageTool: Gender neutrality", MessageFormat, "LanguageTool:GenderNeutrality", DiagnosticSeverity.Warning, isEnabledByDefault: true);
    protected static readonly DiagnosticDescriptor LanguageToolGrammarRule = new DiagnosticDescriptor(LanguageToolGrammarDiagnosticId, "LanguageTool: Grammar", MessageFormat, "LanguageTool:Grammer", DiagnosticSeverity.Warning, isEnabledByDefault: true);
    protected static readonly DiagnosticDescriptor LanguageToolMiscRule = new DiagnosticDescriptor(LanguageToolMiscDiagnosticId, "LanguageTool: Misc", MessageFormat, "LanguageTool:Misc", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "Miscellaneous rules that don't fit elsewhere.");
    protected static readonly DiagnosticDescriptor LanguageToolPunctuationRule = new DiagnosticDescriptor(LanguageToolPunctuationDiagnosticId, "LanguageTool: Punctuation", MessageFormat, "LanguageTool:Punctuation", DiagnosticSeverity.Warning, isEnabledByDefault: true);
    protected static readonly DiagnosticDescriptor LanguageToolRedundancyRule = new DiagnosticDescriptor(LanguageToolRedundancyDiagnosticId, "LanguageTool: Redundancy", MessageFormat, "LanguageTool:Redundancy", DiagnosticSeverity.Warning, isEnabledByDefault: true);
    protected static readonly DiagnosticDescriptor LanguageToolRegionalismsRule = new DiagnosticDescriptor(LanguageToolRegionalismsDiagnosticId, "LanguageTool: Regionalisms", MessageFormat, "LanguageTool:Regionalisms", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "Regionalisms: words used only in another language variant or used with different meanings.");
    protected static readonly DiagnosticDescriptor LanguageToolRepetitionsRule = new DiagnosticDescriptor(LanguageToolRepetitionsDiagnosticId, "LanguageTool: Repetitions", MessageFormat, "LanguageTool:Repetitions", DiagnosticSeverity.Warning, isEnabledByDefault: true);
    protected static readonly DiagnosticDescriptor LanguageToolSemanticsRule = new DiagnosticDescriptor(LanguageToolSemanticsDiagnosticId, "LanguageTool: Semantics", MessageFormat, "LanguageTool:Semantics", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "Logic, content, and consistency problems.");
    protected static readonly DiagnosticDescriptor LanguageToolStyleRule = new DiagnosticDescriptor(LanguageToolStyleDiagnosticId, "LanguageTool: Style", MessageFormat, "LanguageTool:Style", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "General style issues not covered by other categories, like overly verbose wording.");
    protected static readonly DiagnosticDescriptor LanguageToolTypographyRule = new DiagnosticDescriptor(LanguageToolTypographyDiagnosticId, "LanguageTool: Typography", MessageFormat, "LanguageTool:Typography", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "Problems like incorrectly used dash or quote characters.");
    protected static readonly DiagnosticDescriptor LanguageToolTyposRule = new DiagnosticDescriptor(LanguageToolTyposDiagnosticId, "LanguageTool: Typos", MessageFormat, "LanguageTool:Typos", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "Spelling issues.");
    protected static readonly DiagnosticDescriptor LanguageToolWikipediaRule = new DiagnosticDescriptor(LanguageToolWikipediaDiagnosticId, "LanguageTool: Wikipedia", MessageFormat, "LanguageTool:Wikipedia", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "Rules that only make sense when editing Wikipedia (typically turned off by default in LanguageTool).");

    private static readonly Dictionary<string, DiagnosticDescriptor> languageToolCategoryIdToDiagnosticDescriptors = new Dictionary<string, DiagnosticDescriptor>()
    {
      { "CASING", LanguageToolCasingRule },
      { "COLLOQUIALISMS", LanguageToolColloquialismsRule },
      { "COMPOUNDING", LanguageToolCompoundingRule },
      { "CONFUSED_WORDS", LanguageToolConfusedWordsRule },
      { "FALSE_FRIENDS", LanguageToolFalseFriendsRule },
      { "GENDER_NEUTRALITY", LanguageToolGenderNeutralityRule },
      { "GRAMMAR", LanguageToolGrammarRule },
      { "PUNCTUATION", LanguageToolPunctuationRule },
      { "REDUNDANCY", LanguageToolRedundancyRule },
      { "REGIONALISMS", LanguageToolRegionalismsRule },
      { "REPETITIONS", LanguageToolRepetitionsRule },
      { "SEMANTICS", LanguageToolSemanticsRule },
      { "STYLE", LanguageToolStyleRule },
      { "TYPOGRAPHY", LanguageToolTypographyRule },
      { "TYPOS", LanguageToolTyposRule },
      { "WIKIPEDIA", LanguageToolWikipediaRule },
    };

    protected void CheckToken(DiagnosticDescriptor rule, SyntaxNodeAnalysisContext context, SyntaxToken syntaxToken)
    {
      var text = syntaxToken.ValueText;
      if (string.IsNullOrWhiteSpace(text))
      {
        return;
      }

      this.CheckText(rule, text, syntaxToken.GetLocation(), context, LanguagesByRule(rule.Id));
    }

    protected virtual void CheckText(DiagnosticDescriptor rule, string text, Location location, SyntaxNodeAnalysisContext context, IEnumerable<ILanguage> languages)
    {
      this.CheckLine(rule, text, location, context, languages);
    }

    protected void CheckLine(DiagnosticDescriptor rule, string line, Location location, SyntaxNodeAnalysisContext context, IEnumerable<ILanguage> languages)
    {
      if (string.IsNullOrWhiteSpace(line))
      {
        return;
      }

      Logger.Log($"{this.GetType().Name} - CheckLine: [{line}]");
      foreach (var wordMatch in this.splitLineIntoWords.Matches(line).OfType<Match>())
      {
        var wordLocation = Location.Create(context.Node.SyntaxTree, TextSpan.FromBounds(location.SourceSpan.Start + wordMatch.Index, location.SourceSpan.Start + wordMatch.Index + wordMatch.Length));
        this.CheckWord(rule, wordMatch.Value, wordLocation, context, languages);
      }
    }

    protected virtual bool CheckWord(DiagnosticDescriptor rule, string word, Location wordLocation, SyntaxNodeAnalysisContext context, IEnumerable<ILanguage> languages)
    {
      // check if the "word" actually represents a GUID which should not further be parsed
      if (this.isGuid.IsMatch(word))
      {
        return true;
      }

      // check if the word is correct
      if (IsWordCorrect(word, languages))
      {
        return true;
      }

      return false;
    }

    protected static bool IsWordCorrect(string word, IEnumerable<ILanguage> languages)
    {
       return string.IsNullOrWhiteSpace(word)
        || languages == null
        || !languages.Any()
        || languages.Any(language => DictionaryManager.IsWordCorrect(word, language.LocalDictionaryLanguage));
    }

    protected static void ReportWord(DiagnosticDescriptor rule, string word, Location location, SyntaxNodeAnalysisContext context, IEnumerable<ILanguage> languages = null)
    {
      var propertyBagForFixProvider = ImmutableDictionary.Create<string, string>();
      propertyBagForFixProvider = propertyBagForFixProvider.Add("offendingWord", word);
      if (languages != null)
      {
        propertyBagForFixProvider = propertyBagForFixProvider.Add("validLanguages", languages.Select(x => x.LocalDictionaryLanguage).Aggregate(string.Empty, (allSupportedLanguages, supportedLanguage) => allSupportedLanguages + supportedLanguage + ";"));
      }

      var diagnostic = Diagnostic.Create(rule, location, propertyBagForFixProvider, "Possible spelling mistake: " + word);
      context.ReportDiagnostic(diagnostic);
    }

    // result: true is LanguageTool was configured, otherwise false
    protected static bool CheckTextWithLanguageTool(DiagnosticDescriptor rule, Location location, string text, IEnumerable<ILanguage> languages, SyntaxNodeAnalysisContext context)
    {
      var languageToolUriString = AnalyzerContext.SpellcheckSettings.LanguageToolUrl;
      if (!languageToolIsOffline
          && !string.IsNullOrEmpty(languageToolUriString) 
          && Uri.TryCreate(languageToolUriString, UriKind.Absolute, out var languageToolUri))
      {
        foreach (var language in languages)
        {
          var response = LanguageToolClient.Check(languageToolUri, text, language.LanguageToolLanguage);
          if (response == null)
          {
            // seems like an error occured. Disable LanguageTool analysis
            // maybe later just do a timeout and retry after 10 seconds or so
            languageToolIsOffline = true;
            return false;
          }

          Logger.Log($"Ask LanguageTool: [{text}] => {response.Matches.Count} issues found");
          foreach (var match in response.Matches)
          {
            Logger.Log(match);
            try
            {
              var issueLocation = CreateMatchLocation(location, text, match, context, out string textMatch);

              var propertyBagForFixProvider = new Dictionary<string, string>
              {
                { "offendingWord", textMatch },
                { "CategoryId", match.Rule.Category.Id },
                { "LanguageToolRuleId", match.Rule.Id },
                { "LanguageToolRuleIssueType", match.Rule.IssueType }
              };

              var message = BuildLanguageToolDiagnosticMessage(match, propertyBagForFixProvider);

              var properties = ImmutableDictionary.Create<string, string>();
              properties = properties.AddRange(propertyBagForFixProvider);
              if (!languageToolCategoryIdToDiagnosticDescriptors.TryGetValue(match.Rule.Category.Id, out var diagnosticDescriptor))
              {
                diagnosticDescriptor = LanguageToolMiscRule;
              }

              var diagnostic = Diagnostic.Create(diagnosticDescriptor, issueLocation, properties, message);
              context.ReportDiagnostic(diagnostic);
            }
            catch (Exception exception)
            {
              Logger.Log(exception);
            }
          }
        }

        return true;
      }

      return false;
    }

    private static string BuildLanguageToolDiagnosticMessage(LanguageTool.Match match, Dictionary<string, string> propertyBagForFixProvider)
    {
      var suggestionsAsText = new StringBuilder();
      var i = 1;
      foreach (var replacement in match.Replacements)
      {
        ////var suggestion = match.Sentence.Substring(0, match.Offset) + replacement.Value + match.Sentence.Substring(match.Offset + match.Length);
        var suggestion = replacement.Value;
        suggestionsAsText.AppendLine(suggestion);
        propertyBagForFixProvider.Add($"suggestion_{i}", suggestion);
        i++;
      }

      var header = $"{match.Rule.Category.Name}: {match.Rule.Description}";
      var optionalShortMessage = !string.IsNullOrEmpty(match.ShortMessage) ? $"\r\n{match.ShortMessage}" : string.Empty;
      return $"{header}{optionalShortMessage}\r\n{match.Message}{(!string.IsNullOrEmpty(suggestionsAsText.ToString()) ? "\r\nReplace with\r\n" + suggestionsAsText.ToString() : string.Empty)}";
    }

    private static Location CreateMatchLocation(Location location, string text, LanguageTool.Match match, SyntaxNodeAnalysisContext context, out string textMatch)
    {
      // next lines generate the issue location (as an offset from the first character of the first line of the document)
      // Attention: we need to take into account special whitespace characters, for instance "\n" will be counted as 1 character
      // but it's represented in the source file as two characters. So the offset and length of the Location must
      // take this into account and correct
      var textBeforeMatch = text.Substring(0, match.Offset);
      var whitespaceCharactersBeforeMatch = textBeforeMatch.Count(x => char.IsControl(x));
      textMatch = text.Substring(match.Offset, match.Length);
      var whitespaceCharactersInMatch = textMatch.Count(x => char.IsControl(x));
      var startOfMatch = location.SourceSpan.Start + match.Offset + whitespaceCharactersBeforeMatch;
      var matchLength = match.Length + whitespaceCharactersInMatch;
      return Location.Create(context.Node.SyntaxTree, TextSpan.FromBounds(startOfMatch, startOfMatch + matchLength));
    }

    public static IEnumerable<ILanguage> LanguagesByRule(string ruleId)
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