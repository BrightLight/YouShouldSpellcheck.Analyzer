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

    // Diagnostic triggered and checked for
    [Test]
    public void ConsiderEscapeCharactersForLocation()
    {
      var test = @"
    namespace ConsoleApplication1
    {
      using System.ComponentModel.DataAnnotations;

      class TypeName
      {   
        [Display(Name = ""Special \""escapng\"" and \na new lines"")]
        public string Name3 { get; }
      }
    }";

      // make sure that the expected location is correct
      var lineZeroBased= 7;
      var startColumZeroBased = 35;
      var foundLocation = test.Split(new[] { Environment.NewLine }, StringSplitOptions.None)[lineZeroBased].Substring(startColumZeroBased);
      Assert.That(foundLocation, Does.StartWith("escapng"));

      var expected = new DiagnosticResult
      {
        Id = "YS100",
        Message = "Possible spelling mistake: escapng",
        Severity = DiagnosticSeverity.Warning,
        Locations =
          new[] {
            new DiagnosticResultLocation("Test0.cs", lineZeroBased + 1, startColumZeroBased + 1)
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
