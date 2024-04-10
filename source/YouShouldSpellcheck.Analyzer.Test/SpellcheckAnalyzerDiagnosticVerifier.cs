using NUnit.Framework;

namespace YouShouldSpellcheck.Analyzer.Test
{
  using System;
  using System.IO;

  /// <summary>
  /// This class is used to set up the spellchecker settings for all tests.
  /// </summary>
  [SetUpFixture]
  public class SpellcheckAnalyzerDiagnosticVerifier
  {
    /// <summary>
    /// Setup the spellchecker settings.
    /// </summary>
    [OneTimeSetUp]
    public void SetupSpellcheckerSettings()
    {
      var customDictionariesRootFolder = Environment.GetEnvironmentVariable("APPVEYOR_BUILD_FOLDER")
                                     ?? @"d:\projects\YouShouldSpellcheck.Analyzer_cleanup\";
      var customDictionariesFolder = Path.Combine(customDictionariesRootFolder, "dic");

      var languageEnUs = new Language { LocalDictionaryLanguage = "en_US", LanguageToolLanguage = "en-us" };
      var spellcheckerSettings = new SpellcheckSettings()
      {
        MethodNameLanguages = new[] { languageEnUs },
        ClassNameLanguages = new[] { languageEnUs },
        StringLiteralLanguages = new[] { languageEnUs },
        Attributes = new AttributeProperty[] { new AttributeProperty { AttributeName = "DisplayAttribute", PropertyName = "Name", Languages = new[] { languageEnUs } } },
        CustomDictionariesFolder = customDictionariesFolder,
      };

      AnalyzerContext.SpellcheckSettings = new SpellcheckSettingsWrapper(spellcheckerSettings, null);
    }
  }
}
