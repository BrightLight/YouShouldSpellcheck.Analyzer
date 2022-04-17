namespace YouShouldSpellcheck.Analyzer.Test
{
  using System;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.Diagnostics;
  using NUnit.Framework;
  using TestHelper;

  [TestFixture]
  public class StringLiteralSpellcheckAnalyzerDiagnosticTests : SpellcheckAnalyzerDiagnosticVerifier
  {
    // Diagnostic triggered and checked for
    [Test]
    public void TestMethod2()
    {
      var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class TypeNam
        {   
            public string TemperatureMessage = ""Temprature"";
        }
    }";
      var expected = new DiagnosticResult
      {
        Id = "YS101",
        Message = "Possible spelling mistake: Temprature",
        Severity = DiagnosticSeverity.Warning,
        Locations =
          new[] {
            new DiagnosticResultLocation("Test0.cs", 13, 49)
          }
      };

      this.VerifyCSharpDiagnostic(test, expected);
    }

    protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
    {
      this.SetupSpellcheckerSettings();
      return new StringLiteralSpellcheckAnalyzer();
    }
  }
}
