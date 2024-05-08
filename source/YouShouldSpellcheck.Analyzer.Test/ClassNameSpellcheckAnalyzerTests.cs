namespace YouShouldSpellcheck.Analyzer.Test
{
  using System.Diagnostics.CodeAnalysis;
  using System.Threading;
  using System.Threading.Tasks;
  using AnalyzerFromTemplate2019.Test;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.Testing;
  using NUnit.Framework;
  using YouShouldSpellcheck.Analyzer.CodeFixes;

  /// <summary>
  /// Test class for the <see cref="ClassNameSpellcheckAnalyzer"/>.
  /// </summary>
  /// <remarks>
  /// The tested source code is defined as a class field instead of within the individual test methods
  /// because <see cref="StringSyntaxAttribute"/> only works on fields and properties. 
  /// </remarks>
  [TestFixture]
  public class ClassNameSpellcheckAnalyzerTests
  {
    /// <summary>
    /// The source code with a spelling mistake in the class name.
    /// This is used by <see cref="ReportsSpellingMistakesInClassNameWithExpectedMessage"/>.
    /// </summary>
    [StringSyntax("C#")]
    private const string TypoInClassNames = """
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

          // ClassNameSpellcheckAnalyzer should report on nested classes
          private class Persn { };
        }
      }
      """;

    [StringSyntax("C#")]
    private const string TypoInClassName = """
      using System;
      using System.Collections.Generic;
      using System.Linq;
      using System.Text;
      using System.Threading.Tasks;
      using System.Diagnostics;
                                                                                                                          
      namespace ConsoleApplication1
      {
        // 'TypeName' spelled wrong
        class {|YS103:Typ|}Name
        {
          // ClassNameSpellcheckAnalyzer does not report on method names
          public void PrntNow()
          {
          }
        }
      }
      """;

    [StringSyntax("C#")]
    private const string TypoInNestedClassName = """
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
          // ClassNameSpellcheckAnalyzer does not report on method names
          public void PrntNow()
          {
          }
         
          // ClassNameSpellcheckAnalyzer should report on nested classes
          private class {|YS103:Persn|} { };
        }
      }
      """;

    /// <summary>
    /// Test to ensure that the analyzer detects a spelling mistake in the class name.
    /// </summary>
    [TestCase(TypoInClassName)]
    [TestCase(TypoInNestedClassName)]
    public async Task DetectsSpellingMistake(string sourcecode)
    {
      await CSharpAnalyzerVerifier<ClassNameSpellcheckAnalyzer>.VerifyAnalyzerAsync(sourcecode);
    }

    /// <summary>
    /// Test to ensure that the analyzer reports a spelling mistake in a class name with the expected message.
    /// </summary>
    [Test]
    public async Task ReportsSpellingMistakesInClassNameWithExpectedMessage()
    {
      var expected1 = new DiagnosticResult("YS103", DiagnosticSeverity.Warning)
        .WithMessage("Possible spelling mistake: Typ")
        .WithLocation("/0/Test0.cs", 11, 9);

      var expected2 = new DiagnosticResult("YS103", DiagnosticSeverity.Warning)
        .WithMessage("Possible spelling mistake: Persn")
        .WithLocation("/0/Test0.cs", 18, 19);

      var test = new CSharpAnalyzerVerifier<ClassNameSpellcheckAnalyzer>.Test
      {
        TestCode = TypoInClassNames,
        ExpectedDiagnostics = { expected1, expected2 },
      };

      await test.RunAsync(CancellationToken.None);
    }

    /// <summary>
    /// No diagnostics expected for empty string.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task NoDiagnosticsForEmptyString()
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