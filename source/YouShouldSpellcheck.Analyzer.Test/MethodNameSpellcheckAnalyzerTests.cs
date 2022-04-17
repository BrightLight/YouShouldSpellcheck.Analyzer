namespace YouShouldSpellcheck.Analyzer.Test
{
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.Diagnostics;
  using NUnit.Framework;
  using TestHelper;

  [TestFixture]
  public class MethodNameSpellcheckAnalyzerTests : DiagnosticVerifier
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
        class TypeName
        {   
           // 'PrintNow' spelled wrong
           public void PrntNow()
           {
           }
        }
    }";

      var expected = new DiagnosticResult
      {
        Id = "YS104",
        Message = "Possible spelling mistake: Prnt",
        Severity = DiagnosticSeverity.Warning,
        Locations =
          new[] {
            new DiagnosticResultLocation("Test0.cs", 14, 24)
          }
      };

      this.VerifyCSharpDiagnostic(test, expected);
    }

    protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
    {
      var spellcheckerSettings = new SpellcheckSettings()
      {
        MethodNameLanguages = new Language[] { new Language { LocalDictionaryLanguage = "en_US", LanguageToolLanguage = "en-us" } },
        CustomDictionariesFolder = @"c:\projects\YouShouldSpellcheck.Analyzer\dic\",
      };

      AnalyzerContext.SpellcheckSettings = new SpellcheckSettingsWrapper(spellcheckerSettings, null);
      return new MethodNameSpellcheckAnalyzer();
    }
  }
}
