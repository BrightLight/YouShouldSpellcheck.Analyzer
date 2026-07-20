namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Generic;
  using System.Collections.Immutable;
  using System.Linq;
  using System.Text.RegularExpressions;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.Diagnostics;
  using Microsoft.CodeAnalysis.Text;
  using Match = System.Text.RegularExpressions.Match;

  public abstract class SpellcheckAnalyzerBase : DiagnosticAnalyzer
  {
    public const string MessageFormat = "{0}";
    public const string NamingCategory = "Naming";
    public const string CommentCategory = "Comment";
    public const string ContentCategory = "Content";

    // Retained for diagnostic ID compatibility. LanguageTool runs outside the compiler analyzer.
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
    public const string LanguageToolUnavailableDiagnosticId = "YS218";

    private readonly Regex splitLineIntoWords = new(@"((\b[^\s\/]+\b)((?<=\.\w).)?)", RegexOptions.Compiled);
    private readonly Regex isGuid = new(@"[{(]?[0-9A-Fa-f]{8}[-]?([0-9A-Fa-f]{4}[-]?){3}[0-9A-Fa-f]{12}[)}]?", RegexOptions.Compiled);

    protected abstract bool ConsiderEscapedCharacters { get; }

    internal abstract void RegisterActions(CompilationStartAnalysisContext context, CompilationSpellcheckState state);

    private protected void InitializeAnalyzer(AnalysisContext context)
    {
      context.RegisterCompilationStartAction(compilationContext =>
      {
        var state = CompilationSpellcheckState.Create(compilationContext.Options, compilationContext.CancellationToken);
        this.RegisterActions(compilationContext, state);
      });
    }

    private protected void CheckToken(
      DiagnosticDescriptor rule,
      SyntaxNodeAnalysisContext context,
      SyntaxToken syntaxToken,
      CompilationSpellcheckState state)
    {
      var text = syntaxToken.ValueText;
      if (!string.IsNullOrWhiteSpace(text))
      {
        this.CheckText(rule, text, syntaxToken.GetLocation(), context, state.LanguagesByRule(rule.Id), state);
      }
    }

    private protected virtual void CheckText(
      DiagnosticDescriptor rule,
      string text,
      Location location,
      SyntaxNodeAnalysisContext context,
      IEnumerable<ILanguage> languages,
      CompilationSpellcheckState state)
    {
      this.CheckLine(rule, text, location, context, languages, state);
    }

    private protected void CheckLine(
      DiagnosticDescriptor rule,
      string line,
      Location location,
      SyntaxNodeAnalysisContext context,
      IEnumerable<ILanguage> languages,
      CompilationSpellcheckState state,
      Func<int, int>? getSourcePosition = null)
    {
      if (string.IsNullOrWhiteSpace(line))
      {
        return;
      }

      foreach (var wordMatch in this.splitLineIntoWords.Matches(line).OfType<Match>())
      {
        context.CancellationToken.ThrowIfCancellationRequested();
        var start = getSourcePosition?.Invoke(wordMatch.Index)
          ?? location.SourceSpan.Start + this.AdjustLocationForEscapedCharacters(line, wordMatch.Index);
        var end = getSourcePosition?.Invoke(wordMatch.Index + wordMatch.Length)
          ?? location.SourceSpan.Start + this.AdjustLocationForEscapedCharacters(line, wordMatch.Index + wordMatch.Length);
        var wordLocation = Location.Create(context.Node.SyntaxTree, TextSpan.FromBounds(start, end));
        this.CheckWord(rule, wordMatch.Value, wordLocation, context, languages, state);
      }
    }

    protected int AdjustLocationForEscapedCharacters(string line, int location)
    {
      return location;
    }

    private protected virtual bool CheckWord(
      DiagnosticDescriptor rule,
      string word,
      Location wordLocation,
      SyntaxNodeAnalysisContext context,
      IEnumerable<ILanguage> languages,
      CompilationSpellcheckState state)
    {
      return this.isGuid.IsMatch(word) || IsWordCorrect(word, languages, state);
    }

    private static bool IsWordCorrect(string word, IEnumerable<ILanguage> languages, CompilationSpellcheckState state)
    {
      if (string.IsNullOrWhiteSpace(word) || languages == null)
      {
        return true;
      }

      var languageArray = languages as ILanguage[] ?? languages.ToArray();
      return languageArray.Length == 0
        || languageArray.Any(language => state.IsWordCorrect(word, language.LocalDictionaryLanguage));
    }

    private protected static void ReportWord(
      DiagnosticDescriptor rule,
      string word,
      Location location,
      SyntaxNodeAnalysisContext context,
      IEnumerable<ILanguage>? languages,
      CompilationSpellcheckState state)
    {
      var properties = ImmutableDictionary<string, string?>.Empty.Add("offendingWord", word);
      if (languages != null)
      {
        var languageArray = languages.ToArray();
        properties = properties.Add("validLanguages", string.Join(";", languageArray.Select(language => language.LocalDictionaryLanguage)) + ";");
        var suggestionIndex = 1;
        foreach (var language in languageArray)
        {
          foreach (var suggestion in state.Suggest(word, language.LocalDictionaryLanguage))
          {
            properties = properties
              .Add($"localSuggestion_{suggestionIndex}", suggestion)
              .Add($"localSuggestionLanguage_{suggestionIndex}", language.LocalDictionaryLanguage);
            suggestionIndex++;
          }
        }
      }

      var diagnostic = Diagnostic.Create(rule, location, properties, "Possible spelling mistake: " + word);
      state.ReportOrDeferLocalDiagnostic(rule.Id, diagnostic, context);
    }

  }
}
