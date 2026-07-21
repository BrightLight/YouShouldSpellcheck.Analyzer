namespace YouShouldSpellcheck.Analyzer
{
  using Microsoft.CodeAnalysis;

  internal static class ConfigurationDiagnostics
  {
    public const string InvalidConfigurationDiagnosticId = "YS219";

    public static readonly DiagnosticDescriptor InvalidConfiguration = new(
      InvalidConfigurationDiagnosticId,
      "Invalid spellcheck configuration",
      "{0}",
      "Configuration",
      DiagnosticSeverity.Warning,
      isEnabledByDefault: true,
      description: "Reports invalid YouShouldSpellcheck analyzer configuration.",
      customTags: WellKnownDiagnosticTags.CompilationEnd);
  }
}
