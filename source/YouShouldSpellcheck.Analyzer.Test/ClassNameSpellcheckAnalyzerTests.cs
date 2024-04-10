namespace YouShouldSpellcheck.Analyzer.Test
{
  using AnalyzerFromTemplate2019.Test;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.Diagnostics;
  using Microsoft.CodeAnalysis.Testing;
  using NUnit.Framework;
  using System.Threading.Tasks;

  /// <summary>
  /// Test class for the <see cref="ClassNameSpellcheckAnalyzer"/>.
  /// </summary>
  [TestFixture]
  public class ClassNameSpellcheckAnalyzerTests
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
           // 'TypeName' spelled wrong
        class TypName
        {   
           public void PrintNow()
           {
           }
        }
    }";

      var expected = new DiagnosticResult("YS103", DiagnosticSeverity.Warning)
        .WithMessage("Possible spelling mistake: Typ")
        .WithLocation("/0/Test0.cs", 12, 15);

      await CSharpAnalyzerVerifier<ClassNameSpellcheckAnalyzer>.VerifyAnalyzerAsync(test, expected);
    }
  }
}
