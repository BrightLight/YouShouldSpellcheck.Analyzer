using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace AnalyzerFromTemplate2019.Test
{
  public static partial class CSharpAnalyzerVerifier<TAnalyzer>
      where TAnalyzer : DiagnosticAnalyzer, new()
  {
    public class Test : CSharpAnalyzerTest<TAnalyzer, NUnitVerifier>
    {
      public Test()
      {
        foreach (var input in YouShouldSpellcheck.Analyzer.Test.DefaultTestInputs.Get())
        {
          TestState.AdditionalFiles.Add(input);
        }

        TestState.AnalyzerConfigFiles.Add((
          "/YouShouldSpellcheck.globalconfig",
          YouShouldSpellcheck.Analyzer.Test.DefaultTestInputs.GlobalConfig));

        SolutionTransforms.Add((solution, projectId) =>
        {
          var compilationOptions = solution.GetProject(projectId).CompilationOptions;
          compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(
                      compilationOptions.SpecificDiagnosticOptions.SetItems(CSharpVerifierHelper.NullableWarnings));
          solution = solution.WithProjectCompilationOptions(projectId, compilationOptions);
          return solution;
        });
      }
    }
  }
}
