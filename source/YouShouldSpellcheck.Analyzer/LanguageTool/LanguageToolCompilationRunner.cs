namespace YouShouldSpellcheck.Analyzer
{
#pragma warning disable RS2001 // Release tracking cannot infer the category passed through CreateRule; entries are maintained explicitly.
  using System;
  using System.Collections.Concurrent;
  using System.Collections.Generic;
  using System.Collections.Immutable;
  using System.Linq;
  using System.Text;
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.Diagnostics;
  using Microsoft.CodeAnalysis.Text;
  using YouShouldSpellcheck.Analyzer.LanguageTool;

  internal static class LanguageToolCompilationRunner
  {
    private static readonly DiagnosticDescriptor CasingRule = CreateRule(SpellcheckAnalyzerBase.LanguageToolCasingDiagnosticId, "Casing");
    private static readonly DiagnosticDescriptor ColloquialismsRule = CreateRule(SpellcheckAnalyzerBase.LanguageToolColloquialismsDiagnosticId, "Colloquialisms");
    private static readonly DiagnosticDescriptor CompoundingRule = CreateRule(SpellcheckAnalyzerBase.LanguageToolCompoundingDiagnosticId, "Compounding");
    private static readonly DiagnosticDescriptor ConfusedWordsRule = CreateRule(SpellcheckAnalyzerBase.LanguageToolConfusedWordsDiagnosticId, "Confused words");
    private static readonly DiagnosticDescriptor FalseFriendsRule = CreateRule(SpellcheckAnalyzerBase.LanguageToolFalseFriendsDiagnosticId, "False friends");
    private static readonly DiagnosticDescriptor GenderNeutralityRule = CreateRule(SpellcheckAnalyzerBase.LanguageToolGenderNeutralityDiagnosticId, "Gender neutrality");
    private static readonly DiagnosticDescriptor GrammarRule = CreateRule(SpellcheckAnalyzerBase.LanguageToolGrammarDiagnosticId, "Grammar");
    private static readonly DiagnosticDescriptor MiscRule = CreateRule(SpellcheckAnalyzerBase.LanguageToolMiscDiagnosticId, "Miscellaneous");
    private static readonly DiagnosticDescriptor PunctuationRule = CreateRule(SpellcheckAnalyzerBase.LanguageToolPunctuationDiagnosticId, "Punctuation");
    private static readonly DiagnosticDescriptor RedundancyRule = CreateRule(SpellcheckAnalyzerBase.LanguageToolRedundancyDiagnosticId, "Redundancy");
    private static readonly DiagnosticDescriptor RegionalismsRule = CreateRule(SpellcheckAnalyzerBase.LanguageToolRegionalismsDiagnosticId, "Regionalisms");
    private static readonly DiagnosticDescriptor RepetitionsRule = CreateRule(SpellcheckAnalyzerBase.LanguageToolRepetitionsDiagnosticId, "Repetitions");
    private static readonly DiagnosticDescriptor SemanticsRule = CreateRule(SpellcheckAnalyzerBase.LanguageToolSemanticsDiagnosticId, "Semantics");
    private static readonly DiagnosticDescriptor StyleRule = CreateRule(SpellcheckAnalyzerBase.LanguageToolStyleDiagnosticId, "Style");
    private static readonly DiagnosticDescriptor TypographyRule = CreateRule(SpellcheckAnalyzerBase.LanguageToolTypographyDiagnosticId, "Typography");
    private static readonly DiagnosticDescriptor TyposRule = CreateRule(SpellcheckAnalyzerBase.LanguageToolTyposDiagnosticId, "Typos");
    private static readonly DiagnosticDescriptor WikipediaRule = CreateRule(SpellcheckAnalyzerBase.LanguageToolWikipediaDiagnosticId, "Wikipedia");
    private static readonly DiagnosticDescriptor UnavailableRule = new(
      SpellcheckAnalyzerBase.LanguageToolUnavailableDiagnosticId,
      "LanguageTool check could not be completed",
      "LanguageTool check could not be completed: {0}",
      "LanguageTool",
      DiagnosticSeverity.Warning,
      isEnabledByDefault: true,
      description: "The explicitly enabled LanguageTool server could not be reached or returned an invalid response.",
      customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly ImmutableDictionary<string, DiagnosticDescriptor> RulesByCategory =
      new Dictionary<string, DiagnosticDescriptor>(StringComparer.OrdinalIgnoreCase)
      {
        ["CASING"] = CasingRule,
        ["COLLOQUIALISMS"] = ColloquialismsRule,
        ["COMPOUNDING"] = CompoundingRule,
        ["CONFUSED_WORDS"] = ConfusedWordsRule,
        ["FALSE_FRIENDS"] = FalseFriendsRule,
        ["GENDER_NEUTRALITY"] = GenderNeutralityRule,
        ["GRAMMAR"] = GrammarRule,
        ["PUNCTUATION"] = PunctuationRule,
        ["REDUNDANCY"] = RedundancyRule,
        ["REGIONALISMS"] = RegionalismsRule,
        ["REPETITIONS"] = RepetitionsRule,
        ["SEMANTICS"] = SemanticsRule,
        ["STYLE"] = StyleRule,
        ["TYPOGRAPHY"] = TypographyRule,
        ["TYPOS"] = TyposRule,
        ["WIKIPEDIA"] = WikipediaRule,
      }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);

    public static ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
      RulesByCategory.Values.Append(MiscRule).Append(UnavailableRule).Distinct().ToImmutableArray();

    public static void Run(CompilationAnalysisContext context, CompilationSpellcheckState state)
    {
      var candidates = state.GetLanguageToolCandidates();
      if (candidates.IsDefaultOrEmpty)
      {
        return;
      }

      if (!Uri.TryCreate(state.Settings.LanguageToolUrl, UriKind.Absolute, out var uri)
        || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
      {
        context.ReportDiagnostic(Diagnostic.Create(UnavailableRule, Location.None, "the configured URL is invalid"));
        return;
      }

      try
      {
        foreach (var diagnostic in RunAsync(context.CancellationToken, state, candidates, uri).GetAwaiter().GetResult())
        {
          context.ReportDiagnostic(diagnostic);
        }
      }
      catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (Exception exception)
      {
        context.ReportDiagnostic(Diagnostic.Create(UnavailableRule, Location.None, exception.Message));
      }
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAsync(CancellationToken cancellationToken, CompilationSpellcheckState state, ImmutableArray<LanguageToolCandidate> candidates, Uri uri)
    {
      using var client = new LanguageToolClient(uri, TimeSpan.FromSeconds(state.Settings.LanguageToolTimeoutSeconds));
      using var gate = new SemaphoreSlim(state.Settings.LanguageToolMaxConcurrency);
      var diagnostics = new ConcurrentQueue<Diagnostic>();
      var tasks = candidates.SelectMany(candidate => candidate.Languages.Select(language =>
        CheckCandidateAsync(cancellationToken, client, gate, candidate, language, diagnostics))).ToArray();
      await Task.WhenAll(tasks).ConfigureAwait(false);
      return diagnostics.ToImmutableArray();
    }

    private static async Task CheckCandidateAsync(CancellationToken cancellationToken, LanguageToolClient client, SemaphoreSlim gate, LanguageToolCandidate candidate, string language, ConcurrentQueue<Diagnostic> diagnostics)
    {
      await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
      try
      {
        var response = await client.CheckAsync(candidate.Text, language, cancellationToken).ConfigureAwait(false);
        foreach (var match in response.Matches)
        {
          cancellationToken.ThrowIfCancellationRequested();
          var diagnostic = CreateDiagnostic(candidate, match);
          if (diagnostic != null)
          {
            diagnostics.Enqueue(diagnostic);
          }
        }
      }
      finally
      {
        gate.Release();
      }
    }

    private static Diagnostic? CreateDiagnostic(LanguageToolCandidate candidate, LanguageToolMatch match)
    {
      if (match.Offset < 0 || match.Length < 0 || match.Offset + match.Length > candidate.Text.Length)
      {
        return null;
      }

      var categoryId = match.Rule?.Category?.Id ?? string.Empty;
      var descriptor = RulesByCategory.TryGetValue(categoryId, out var mappedRule) ? mappedRule : MiscRule;
      var offendingText = candidate.Text.Substring(match.Offset, match.Length);
      var properties = ImmutableDictionary.CreateBuilder<string, string?>();
      properties["offendingWord"] = offendingText;
      properties["CategoryId"] = categoryId;
      properties["LanguageToolRuleId"] = match.Rule?.Id;
      properties["LanguageToolRuleIssueType"] = match.Rule?.IssueType;

      var suggestions = new StringBuilder();
      var suggestionNumber = 1;
      foreach (var replacement in match.Replacements.Where(replacement => !string.IsNullOrEmpty(replacement.Value)))
      {
        properties[$"suggestion_{suggestionNumber++}"] = replacement.Value;
        suggestions.AppendLine(replacement.Value);
      }

      var message = match.Message ?? "LanguageTool reported an issue.";
      if (!string.IsNullOrEmpty(match.ShortMessage))
      {
        message = match.ShortMessage + "\r\n" + message;
      }

      if (suggestions.Length > 0)
      {
        message += "\r\nReplace with\r\n" + suggestions;
      }

      var start = candidate.Location.SourceSpan.Start + match.Offset;
      var location = candidate.Location.SourceTree == null
        ? candidate.Location
        : Location.Create(candidate.Location.SourceTree, new TextSpan(start, match.Length));
      return Diagnostic.Create(descriptor, location, properties.ToImmutable(), message);
    }

    private static DiagnosticDescriptor CreateRule(string id, string category) => new(
      id,
      "LanguageTool: " + category,
      SpellcheckAnalyzerBase.MessageFormat,
      "LanguageTool",
      DiagnosticSeverity.Warning,
      isEnabledByDefault: true,
      customTags: WellKnownDiagnosticTags.CompilationEnd);
  }
#pragma warning restore RS2001
}
