namespace YouShouldSpellcheck.Analyzer.Test
{
  using System;
  using System.Collections.Immutable;
  using System.Threading;
  using System.Threading.Tasks;
  using AnalyzerFromTemplate2019.Test;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.Diagnostics;
  using Microsoft.CodeAnalysis.Testing;
  using NUnit.Framework;

  [TestFixture]
  public class StringLiteralSpellcheckAnalyzerDiagnosticTests
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
        class TypeNam
        {   
            public string TemperatureMessage = ""Temprature"";
        }
    }";
      var expected = new DiagnosticResult("YS101", DiagnosticSeverity.Warning)
        .WithMessage("Possible spelling mistake: Temprature")
        .WithLocation("/0/Test0.cs", 13, 49);

      SpellcheckAnalyzerDiagnosticVerifier.SetupSpellcheckerSettings();
      await CSharpAnalyzerVerifier<StringLiteralSpellcheckAnalyzer>.VerifyAnalyzerAsync(test, expected);
    }

    // Diagnostic triggered and checked for
    [Test]
    public async Task ConsiderEscapeCharactersForLocation()
    {
      var source = @"
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
      var lineZeroBased = 7;
      var startColumZeroBased = 35;
      var foundLocation = source.Split(new[] { Environment.NewLine }, StringSplitOptions.None)[lineZeroBased].Substring(startColumZeroBased);
      Assert.That(foundLocation, Does.StartWith("escapng"));

      var expected = new DiagnosticResult("YS100", DiagnosticSeverity.Warning)
        .WithMessage("Possible spelling mistake: escapng")
        .WithLocation("/0/Test0.cs", lineZeroBased + 1, startColumZeroBased + 1);

      SpellcheckAnalyzerDiagnosticVerifier.SetupSpellcheckerSettings();

      // next lines are basically whare VerifyAnalyzerAsync(test, expected) does
      // but we need to add a ReferenceAssemblies
      var test = new CSharpAnalyzerVerifier<StringLiteralSpellcheckAnalyzer>.Test()
      {
        ReferenceAssemblies = ReferenceAssemblies.Default.AddAssemblies(ImmutableArray.Create("System.ComponentModel.DataAnnotations")),
        TestCode = source,
      };

      test.ExpectedDiagnostics.Add(expected);
      await test.RunAsync(CancellationToken.None);
    }
  }
}
