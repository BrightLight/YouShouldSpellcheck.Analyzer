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
    public const string AttributeArgumentStringDiagnosticId = "YS100";

    // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
    // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
    public const string Title = "Spelling error";

    public const string MessageFormat = "{0}";

    private const string AttributeArgumentRuleDescription = "Attribute argument should be spelled correctly.";

    public const string NamingCategory = "Naming";

    public const string CommentCategory = "Comment";

    public const string ContentCategory = "Content";

    private static readonly DiagnosticDescriptor AttributeArgumentStringRule = new DiagnosticDescriptor(AttributeArgumentStringDiagnosticId, Title, MessageFormat, ContentCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: AttributeArgumentRuleDescription);

    private static bool languageToolIsOffline;

    // See http://stackoverflow.com/questions/7311734/split-sentence-into-words-but-having-trouble-with-the-punctuations-in-c-sharp
    // private readonly Regex splitLineIntoWords = new Regex(@"((\b[^\s.]+\b)((?<=\.\w).)?)", RegexOptions.Compiled); // original with additional "." (for reasons I no longer no) 
    private readonly Regex splitLineIntoWords = new Regex(@"((\b[^\s\/]+\b)((?<=\.\w).)?)", RegexOptions.Compiled); // version from StackOverflow modified to split forwared dash as well (e.g. "sender/receiver").

    private readonly Regex isGuid = new Regex(@"[{(]?[0-9A-Fa-f]{8}[-]?([0-9A-Fa-f]{4}[-]?){3}[0-9A-Fa-f]{12}[)}]?", RegexOptions.Compiled);


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

    protected static void ReportWord(DiagnosticDescriptor rule, string word, Location location, SyntaxNodeAnalysisContext context)
    {
      var propertyBagForFixProvider = ImmutableDictionary.Create<string, string>();
      propertyBagForFixProvider = propertyBagForFixProvider.Add("offendingWord", word);
      var diagnostic = Diagnostic.Create(rule, location, propertyBagForFixProvider, "Spelling error: " + word);
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
              var diagnostic = Diagnostic.Create(rule, issueLocation, properties, message);
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