namespace YouShouldSpellcheck.Analyzer.Test
{
  using AnalyzerFromTemplate2019.Test;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.Diagnostics;
  using Microsoft.CodeAnalysis.Testing;
  using NUnit.Framework;
  using System.Threading;
  using System.Threading.Tasks;

  [TestFixture]
  public class MethodNameSpellcheckAnalyzerTests
  {
    // Diagnostic triggered and checked for
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
        class TypeName
        {   
           // 'PrintNow' spelled wrong
           public void PrntNow()
           {
           }
        }
    }";

      var expected = new DiagnosticResult("YS104", DiagnosticSeverity.Warning)
        .WithMessage("Possible spelling mistake: Prnt")
        .WithLocation("/0/Test0.cs", 14, 24);

      SpellcheckAnalyzerDiagnosticVerifier.SetupSpellcheckerSettings();
      await CSharpAnalyzerVerifier<MethodNameSpellcheckAnalyzer>.VerifyAnalyzerAsync(test, expected);
    }

    //No diagnostics expected to show up
    [Test]
    public async Task TestMethod1()
    {
      var test = @"";

      SpellcheckAnalyzerDiagnosticVerifier.SetupSpellcheckerSettings();
      await CSharpAnalyzerVerifier<MethodNameSpellcheckAnalyzer>.VerifyAnalyzerAsync(test);
    }

    //Diagnostic and CodeFix both triggered and checked for
    [Test]
    public async Task TestMethod3()
    {
      var source = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
          public void {|YS104:Metod|}Name()
          {
          }
        }
    }";


    var fixedSource = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
          public void MethodName()
          {
          }
        }
    }";

      SpellcheckAnalyzerDiagnosticVerifier.SetupSpellcheckerSettings();

      var test = new CSharpCodeFixVerifier<MethodNameSpellcheckAnalyzer, MethodNameCodeFixProvider>.Test
      {
        TestCode = source,
        FixedCode = fixedSource,
        CodeActionIndex = 1,
      };

      test.ExpectedDiagnostics.AddRange(DiagnosticResult.EmptyDiagnosticResults);
      await test.RunAsync(CancellationToken.None);
    }
  }
}
