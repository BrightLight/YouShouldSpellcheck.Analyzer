namespace YouShouldSpellcheck.Analyzer.Test
{
  using AnalyzerFromTemplate2019.Test;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CodeFixes;
  using Microsoft.CodeAnalysis.Diagnostics;
  using Microsoft.CodeAnalysis.Testing;
  using NUnit.Framework;
  using YouShouldSpellcheck.Analyzer;

  [TestFixture]
  public class UnitTest
  {

    //No diagnostics expected to show up
    [Test]
    public void TestMethod1()
    {
      var test = @"";

      CSharpAnalyzerVerifier<ClassNameSpellcheckAnalyzer>.VerifyAnalyzerAsync(test);
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
      var expected = new DiagnosticResult("YS103", DiagnosticSeverity.Warning)
      .WithMessage("Possible spelling mistake: Typ")
      .WithLocation("Test0.cs", 12, 15);

      CSharpAnalyzerVerifier<ClassNameSpellcheckAnalyzer>.VerifyAnalyzerAsync(test, expected);

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

      ////this.SetupSpellcheckerSettings();
      CSharpCodeFixVerifier<ClassNameSpellcheckAnalyzer, ClassNameCodeFixProvider>.VerifyCodeFixAsync(test, fixtest);
    }
  }
}