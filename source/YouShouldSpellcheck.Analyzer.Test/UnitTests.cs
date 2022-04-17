namespace YouShouldSpellcheck.Analyzer.Test
{
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CodeFixes;
  using Microsoft.CodeAnalysis.Diagnostics;
  using NUnit.Framework;
  using System;
  using TestHelper;
  using YouShouldSpellcheck.Analyzer;

  [TestFixture]
  public class UnitTest : CodeFixVerifier
  {

    //No diagnostics expected to show up
    [Test]
    public void TestMethod1()
    {
      var test = @"";

      VerifyCSharpDiagnostic(test);
    }

    //Diagnostic and CodeFix both triggered and checked for
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

      VerifyCSharpDiagnostic(test, expected);

      var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        // 'TypeName' spelled wrong
        class TypeName
        {   
        }
    }";
      VerifyCSharpFix(test, fixtest, 5);
    }

    protected override CodeFixProvider GetCSharpCodeFixProvider()
    {
      return new ClassNameCodeFixProvider();
    }

    protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
    {
      this.SetupSpellcheckerSettings();
      return new ClassNameSpellcheckAnalyzer();
    }
  }
}