namespace YouShouldSpellcheck.Analyzer
{
  using System.Collections.Immutable;
  using System.Linq;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.Diagnostics;

  /// <summary>
  /// Coordinates all spellchecking rules around one state object per compilation.
  /// </summary>
  [DiagnosticAnalyzer(LanguageNames.CSharp)]
  public sealed class YouShouldSpellcheckDiagnosticAnalyzer : DiagnosticAnalyzer
  {
    private readonly ImmutableArray<SpellcheckAnalyzerBase> analyzers =
    [
      new ClassNameSpellcheckAnalyzer(),
      new MethodNameSpellcheckAnalyzer(),
      new VariableNameSpellcheckAnalyzer(),
      new PropertyNameSpellcheckAnalyzer(),
      new EnumNameSpellcheckAnalyzer(),
      new EnumMemberNameSpellcheckAnalyzer(),
      new EventNameSpellcheckAnalyzer(),
      new XmlTextSpellcheckAnalyzer(),
      new StringLiteralSpellcheckAnalyzer(),
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
      this.analyzers.SelectMany(analyzer => analyzer.SupportedDiagnostics).ToImmutableArray();

    public override void Initialize(AnalysisContext context)
    {
      context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
      context.EnableConcurrentExecution();
      context.RegisterCompilationStartAction(compilationContext =>
      {
        var state = CompilationSpellcheckState.Create(compilationContext.Options, compilationContext.CancellationToken);
        foreach (var analyzer in this.analyzers)
        {
          analyzer.RegisterActions(compilationContext, state);
        }
      });
    }
  }
}
