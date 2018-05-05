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
  using RestSharp.Extensions;
  using YouShouldSpellcheck.Analyzer.LanguageTool;
  using Match = System.Text.RegularExpressions.Match;

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

    protected void CheckToken(DiagnosticDescriptor rule, SyntaxNodeAnalysisContext context, SyntaxToken syntaxToken)
    {
      var text = syntaxToken.ValueText;
      if (string.IsNullOrWhiteSpace(text))
      {
        return;
      }

      this.CheckText(rule, text, syntaxToken.GetLocation(), context, LanguagesByRule(rule.Id));
    }

    protected virtual void CheckText(DiagnosticDescriptor rule, string text, Location location, SyntaxNodeAnalysisContext context, string[] languages)
    {
      this.CheckLine(rule, text, location, context, languages);
    }

    protected void CheckLine(DiagnosticDescriptor rule, string line, Location location, SyntaxNodeAnalysisContext context, string[] languages)
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

    protected virtual bool CheckWord(DiagnosticDescriptor rule, string word, Location wordLocation, SyntaxNodeAnalysisContext context, string[] languages)
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

    protected static void CheckTextWithLanguageTool(DiagnosticDescriptor rule, Location location, string text, string[] languages, SyntaxNodeAnalysisContext context)
    {
      var languageToolUriString = AnalyzerContext.SpellcheckSettings.LanguageToolUrl;
      if (Uri.TryCreate(languageToolUriString, UriKind.Absolute, out var languageToolUri))
      {
        foreach (var language in languages)
        {
          // temporary hack because dictionaries use other language "code" than language tool :-/
          var languageMapping = new Dictionary<string, string>
          {
            { "de_DE_frami", "de-DE" },
            { "en_US", "en-US" }
          };
          var languageToolLanguage = languageMapping[language];

          var response = LanguageToolClient.Check(languageToolUri, text, languageToolLanguage);
          foreach (var match in response.Matches)
          {
            var issueLocation = Location.Create(context.Node.SyntaxTree, TextSpan.FromBounds(location.SourceSpan.Start + match.Offset, location.SourceSpan.Start + match.Offset + match.Length));
            var propertyBagForFixProvider = ImmutableDictionary.Create<string, string>();
            propertyBagForFixProvider = propertyBagForFixProvider.Add("offendingWord", match.Context.Text.Substring(match.Offset, match.Length));

            var suggestionsAsText = new StringBuilder();
            var i = 1;
            foreach (var replacement in match.Replacements)
            {
              var suggestion = match.Sentence.Substring(0, match.Offset) + replacement.Value + match.Sentence.Substring(match.Offset + match.Length);
              suggestionsAsText.AppendLine(suggestion);
              propertyBagForFixProvider = propertyBagForFixProvider.Add($"suggestion_{i}", suggestion);
              i++;
            }

            var message = $"{match.ShortMessage}: {match.Rule.Description}\r\n{match.Message}\r\nReplace with\r\n{suggestionsAsText}";

            var diagnostic = Diagnostic.Create(rule, issueLocation, propertyBagForFixProvider, message);
            context.ReportDiagnostic(diagnostic);
          }
        }
      }
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