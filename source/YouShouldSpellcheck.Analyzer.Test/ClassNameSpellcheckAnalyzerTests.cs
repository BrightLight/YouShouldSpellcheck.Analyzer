namespace YouShouldSpellcheck.Analyzer.Test
{
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.Diagnostics;
  using NUnit.Framework;
  using TestHelper;

  [TestFixture]
  public class ClassNameSpellcheckAnalyzerTests : SpellcheckAnalyzerDiagnosticVerifier
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
           // 'TypeName' spelled wrong
        class TypName
        {   
           public void PrintNow()
           {
           }
        }
    }";

      var expected = new DiagnosticResult
      {
        Id = "YS103",
        Message = "Possible spelling mistake: Typ",
        Severity = DiagnosticSeverity.Warning,
        Locations =
          new[] {
            new DiagnosticResultLocation("Test0.cs", 12, 15)
          }
      };

      this.VerifyCSharpDiagnostic(test, expected);
    }

    protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
    {
      this.SetupSpellcheckerSettings();
      return new ClassNameSpellcheckAnalyzer();
    }
  }
}
