namespace YouShouldSpellcheck.Analyzer.Test
{
  using AnalyzerFromTemplate2019.Test;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.Testing;
  using NUnit.Framework;
  using YouShouldSpellcheck.Analyzer;
  using YouShouldSpellcheck.Analyzer.CodeFixes;
  using System.Threading;
  using System.Threading.Tasks;

  [TestFixture]
  public class UnitTest
  {
    //No diagnostics expected to show up
    [Test]
    public async Task TestMethod1()
    {
      var test = @"";

      await CSharpAnalyzerVerifier<ClassNameSpellcheckAnalyzer>.VerifyAnalyzerAsync(test);
    }

    //Diagnostic and CodeFix both triggered and checked for
    [Test]
    public async Task TestMethod2()
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
        .WithSpan("/0/Test0.cs", 12, 15, 12, 18);
      ////.WithLocation("Test0.cs", 12, 15);

      await CSharpAnalyzerVerifier<ClassNameSpellcheckAnalyzer>.VerifyAnalyzerAsync(test, expected);

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

      var testIt = new CSharpCodeFixVerifier<ClassNameSpellcheckAnalyzer, ClassNameCodeFixProvider>.Test
      {
        TestCode = test,
        FixedCode = fixtest,
        CodeActionIndex = 6,
      };

      testIt.ExpectedDiagnostics.Add(expected);
      await testIt.RunAsync(CancellationToken.None);
      
    }
  }
}