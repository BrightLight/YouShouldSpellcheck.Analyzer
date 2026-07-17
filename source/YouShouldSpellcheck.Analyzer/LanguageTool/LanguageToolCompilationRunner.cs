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
    private static readonly DiagnosticDescriptor CasingRule = CreateRule(
      SpellcheckAnalyzerBase.LanguageToolCasingDiagnosticId,
      "Casing",
      "Reports incorrect capitalization, such as uppercase text where lowercase is required or vice versa.");
    private static readonly DiagnosticDescriptor ColloquialismsRule = CreateRule(
      SpellcheckAnalyzerBase.LanguageToolColloquialismsDiagnosticId,
      "Colloquialisms",
      "Reports colloquial or overly informal language.");
    private static readonly DiagnosticDescriptor CompoundingRule = CreateRule(
      SpellcheckAnalyzerBase.LanguageToolCompoundingDiagnosticId,
      "Compounding",
      "Reports compound terms that should be joined, separated, or hyphenated differently.");
    private static readonly DiagnosticDescriptor ConfusedWordsRule = CreateRule(
      SpellcheckAnalyzerBase.LanguageToolConfusedWordsDiagnosticId,
      "Confused words",
      "Reports words that are easily confused and do not fit their context.");
    private static readonly DiagnosticDescriptor FalseFriendsRule = CreateRule(
      SpellcheckAnalyzerBase.LanguageToolFalseFriendsDiagnosticId,
      "False friends",
      "Reports words that language learners may confuse with similar words from another language.");
    private static readonly DiagnosticDescriptor GenderNeutralityRule = CreateRule(
      SpellcheckAnalyzerBase.LanguageToolGenderNeutralityDiagnosticId,
      "Gender neutrality",
      "Reports wording that may not be gender-neutral.");
    private static readonly DiagnosticDescriptor GrammarRule = CreateRule(
      SpellcheckAnalyzerBase.LanguageToolGrammarDiagnosticId,
      "Grammar",
      "Reports grammatical errors detected by LanguageTool.");
    private static readonly DiagnosticDescriptor MiscRule = CreateRule(
      SpellcheckAnalyzerBase.LanguageToolMiscDiagnosticId,
      "Miscellaneous",
      "Reports miscellaneous LanguageTool issues and issues whose categories are not mapped to another YS2xx rule.");
    private static readonly DiagnosticDescriptor PunctuationRule = CreateRule(
      SpellcheckAnalyzerBase.LanguageToolPunctuationDiagnosticId,
      "Punctuation",
      "Reports missing, incorrect, or unnecessary punctuation.");
    private static readonly DiagnosticDescriptor RedundancyRule = CreateRule(
      SpellcheckAnalyzerBase.LanguageToolRedundancyDiagnosticId,
      "Redundancy",
      "Reports redundant words or phrases.");
    private static readonly DiagnosticDescriptor RegionalismsRule = CreateRule(
      SpellcheckAnalyzerBase.LanguageToolRegionalismsDiagnosticId,
      "Regionalisms",
      "Reports terms that are inappropriate for the selected regional language variant or have a different meaning in it.");
    private static readonly DiagnosticDescriptor RepetitionsRule = CreateRule(
      SpellcheckAnalyzerBase.LanguageToolRepetitionsDiagnosticId,
      "Repetitions",
      "Reports unnecessarily repeated words or phrases.");
    private static readonly DiagnosticDescriptor SemanticsRule = CreateRule(
      SpellcheckAnalyzerBase.LanguageToolSemanticsDiagnosticId,
      "Semantics",
      "Reports logic, content, or consistency problems.");
    private static readonly DiagnosticDescriptor StyleRule = CreateRule(
      SpellcheckAnalyzerBase.LanguageToolStyleDiagnosticId,
      "Style",
      "Reports general style issues not covered by another LanguageTool category.");
    private static readonly DiagnosticDescriptor TypographyRule = CreateRule(
      SpellcheckAnalyzerBase.LanguageToolTypographyDiagnosticId,
      "Typography",
      "Reports violations of typographic conventions, such as incorrect dash or quotation mark usage.");
    private static readonly DiagnosticDescriptor TyposRule = CreateRule(
      SpellcheckAnalyzerBase.LanguageToolTyposDiagnosticId,
      "Typos",
      "Reports spelling errors detected by LanguageTool.");
    private static readonly DiagnosticDescriptor WikipediaRule = CreateRule(
      SpellcheckAnalyzerBase.LanguageToolWikipediaDiagnosticId,
      "Wikipedia",
      "Reports issues from LanguageTool rules intended specifically for Wikipedia content.");
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
        if (state.LanguageToolAutoFallback)
        {
          ReportFallbackDiagnostics(context, state);
        }
        else
        {
          context.ReportDiagnostic(Diagnostic.Create(UnavailableRule, Location.None, "the configured URL is invalid"));
        }

        return;
      }

      try
      {
        var result = RunAsync(context.CancellationToken, state, candidates, uri).GetAwaiter().GetResult();
        if (state.LanguageToolAutoFallback && !result.Errors.IsDefaultOrEmpty)
        {
          ReportFallbackDiagnostics(context, state);
          return;
        }

        foreach (var diagnostic in result.Diagnostics)
        {
          context.ReportDiagnostic(diagnostic);
        }

        if (!result.Errors.IsDefaultOrEmpty)
        {
          var message = $"{result.Errors.Length} of {result.RequestCount} requests failed; first error: {result.Errors[0]}";
          context.ReportDiagnostic(Diagnostic.Create(UnavailableRule, Location.None, message));
        }
      }
      catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (Exception exception)
      {
        if (state.LanguageToolAutoFallback)
        {
          ReportFallbackDiagnostics(context, state);
        }
        else
        {
          context.ReportDiagnostic(Diagnostic.Create(UnavailableRule, Location.None, exception.Message));
        }
      }
    }

    private static void ReportFallbackDiagnostics(CompilationAnalysisContext context, CompilationSpellcheckState state)
    {
      foreach (var diagnostic in state.GetLanguageToolFallbackDiagnostics())
      {
        context.ReportDiagnostic(diagnostic);
      }
    }

    private static async Task<LanguageToolRunResult> RunAsync(CancellationToken cancellationToken, CompilationSpellcheckState state, ImmutableArray<LanguageToolCandidate> candidates, Uri uri)
    {
      using var client = new LanguageToolClient(uri, TimeSpan.FromSeconds(state.Settings.LanguageToolTimeoutSeconds));
      using var gate = new SemaphoreSlim(state.Settings.LanguageToolMaxConcurrency);
      var diagnostics = new ConcurrentQueue<Diagnostic>();
      var requests = candidates.SelectMany(candidate => candidate.Languages.Select(language =>
        new LanguageToolRequest(candidate, language))).ToArray();
      LanguageToolRequestResult[] requestResults;
      var attemptedRequestCount = requests.Length;
      if (state.LanguageToolAutoFallback)
      {
        var probeResult = await CheckCandidateAsync(cancellationToken, client, gate, requests[0].Candidate, requests[0].Language).ConfigureAwait(false);
        if (probeResult.Error != null)
        {
          requestResults = [probeResult];
          attemptedRequestCount = 1;
        }
        else
        {
          var remainingTasks = requests.Skip(1).Select(request =>
            CheckCandidateAsync(cancellationToken, client, gate, request.Candidate, request.Language)).ToArray();
          var remainingResults = await Task.WhenAll(remainingTasks).ConfigureAwait(false);
          requestResults = [probeResult, .. remainingResults];
        }
      }
      else
      {
        var tasks = requests.Select(request =>
          CheckCandidateAsync(cancellationToken, client, gate, request.Candidate, request.Language)).ToArray();
        requestResults = await Task.WhenAll(tasks).ConfigureAwait(false);
      }

      var errors = ImmutableArray.CreateBuilder<string>();

      foreach (var candidateResults in requestResults.GroupBy(result => result.Candidate, LanguageToolCandidateComparer.Instance))
      {
        var results = candidateResults.ToArray();
        var failedResults = results.Where(result => result.Error != null).ToArray();
        if (failedResults.Length > 0)
        {
          errors.AddRange(failedResults.Select(result => result.Error!));
          continue;
        }

        var responses = results.Select(result => result.Response!).ToArray();
        foreach (var match in responses[0].Matches
          .GroupBy(match => Tuple.Create(match.Offset, match.Length))
          .Select(group => group.First())
          .Where(match => responses.Skip(1).All(response => response.Matches.Any(other => other.Offset == match.Offset && other.Length == match.Length))))
        {
          cancellationToken.ThrowIfCancellationRequested();
          var diagnostic = CreateDiagnostic(candidateResults.Key, match);
          if (diagnostic != null)
          {
            diagnostics.Enqueue(diagnostic);
          }
        }
      }

      return new LanguageToolRunResult(diagnostics.ToImmutableArray(), errors.ToImmutable(), attemptedRequestCount);
    }

    private static async Task<LanguageToolRequestResult> CheckCandidateAsync(CancellationToken cancellationToken, LanguageToolClient client, SemaphoreSlim gate, LanguageToolCandidate candidate, string language)
    {
      await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
      try
      {
        var response = await client.CheckAsync(candidate.Text, language, cancellationToken).ConfigureAwait(false);
        return new LanguageToolRequestResult(candidate, response, null);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (Exception exception)
      {
        return new LanguageToolRequestResult(candidate, null, exception.Message);
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

    private static DiagnosticDescriptor CreateRule(string id, string category, string description) => new(
      id,
      "LanguageTool: " + category,
      SpellcheckAnalyzerBase.MessageFormat,
      "LanguageTool",
      DiagnosticSeverity.Warning,
      isEnabledByDefault: true,
      description: description,
      customTags: WellKnownDiagnosticTags.CompilationEnd);

    private sealed class LanguageToolRequestResult
    {
      public LanguageToolRequestResult(LanguageToolCandidate candidate, LanguageToolResponse? response, string? error)
      {
        this.Candidate = candidate;
        this.Response = response;
        this.Error = error;
      }

      public LanguageToolCandidate Candidate { get; }

      public LanguageToolResponse? Response { get; }

      public string? Error { get; }
    }

    private sealed class LanguageToolRequest
    {
      public LanguageToolRequest(LanguageToolCandidate candidate, string language)
      {
        this.Candidate = candidate;
        this.Language = language;
      }

      public LanguageToolCandidate Candidate { get; }

      public string Language { get; }
    }

    private sealed class LanguageToolRunResult
    {
      public LanguageToolRunResult(ImmutableArray<Diagnostic> diagnostics, ImmutableArray<string> errors, int requestCount)
      {
        this.Diagnostics = diagnostics;
        this.Errors = errors;
        this.RequestCount = requestCount;
      }

      public ImmutableArray<Diagnostic> Diagnostics { get; }

      public ImmutableArray<string> Errors { get; }

      public int RequestCount { get; }
    }
  }
#pragma warning restore RS2001
}
