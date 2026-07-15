using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace AnalyzerFromTemplate2019.Test
{
  public static partial class CSharpCodeFixVerifier<TAnalyzer, TCodeFix>
      where TAnalyzer : DiagnosticAnalyzer, new()
      where TCodeFix : CodeFixProvider, new()
  {
    public class Test : CSharpCodeFixTest<TAnalyzer, TCodeFix, NUnitVerifier>
    {
      public Test()
      {
        foreach (var input in YouShouldSpellcheck.Analyzer.Test.DefaultTestInputs.Get())
        {
          TestState.AdditionalFiles.Add(input);
        }

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
