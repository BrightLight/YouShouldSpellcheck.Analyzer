namespace YouShouldSpellcheck.Analyzer.Test
{
  using System.Threading.Tasks;
  using AnalyzerFromTemplate2019.Test;
  using NUnit.Framework;

  [TestFixture]
  public class PropertyNameSpellcheckAnalyzerTests
  {
    [Test]
    public async Task ReportsMisspelledIdentifierPrefixBeforeNumericSuffix()
    {
      const string source = """
        class MealPlanner
        {
          public string {|YS105:Namez|}1 { get; }
        }
        """;

      await CSharpAnalyzerVerifier<PropertyNameSpellcheckAnalyzer>.VerifyAnalyzerAsync(source);
    }
  }
}
