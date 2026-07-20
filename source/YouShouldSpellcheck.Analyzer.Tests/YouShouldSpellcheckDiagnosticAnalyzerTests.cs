namespace YouShouldSpellcheck.Analyzer.Test
{
  using System.Threading.Tasks;
  using AnalyzerFromTemplate2019.Test;
  using NUnit.Framework;

  [TestFixture]
  public class YouShouldSpellcheckDiagnosticAnalyzerTests
  {
    [Test]
    public async Task ReportsMisspelledCompoundAndNumericIdentifierParts()
    {
      const string source = """
        class MealPlanner
        {
          public string {|YS105:Namez|}1 { get; }

          public string {|YS104:Prepate|}Meal()
          {
            return string.Empty;
          }
        }
        """;

      await CSharpAnalyzerVerifier<YouShouldSpellcheckDiagnosticAnalyzer>.VerifyAnalyzerAsync(source);
    }
  }
}
