namespace YouShouldSpellcheck.Analyzer.Test
{
  using System;
  using System.IO;
  using NUnit.Framework;

  /// <summary>
  /// This class is used to set up the spellchecker settings for all tests.
  /// </summary>
  [SetUpFixture]
  public class SetupTestEnvironment
  {
    /// <summary>
    /// Setup the spellchecker settings.
    /// </summary>
    [OneTimeSetUp]
    public void SetupSpellcheckerSettings()
    {
      var customDictionariesFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "dic");

      var languageEnUs = new Language { LocalDictionaryLanguage = "en_US", LanguageToolLanguage = "en-us" };
      var spellcheckerSettings = new SpellcheckSettings
      {
        MethodNameLanguages = [languageEnUs],
        ClassNameLanguages = [languageEnUs],
        StringLiteralLanguages = [languageEnUs],
        Attributes =
        [
          new AttributeProperty
          {
            AttributeName = "DisplayAttribute",
            PropertyName = "Name",
            Languages = [languageEnUs]
          }
        ],
        CustomDictionariesFolder = customDictionariesFolder,
      };

      AnalyzerContext.SpellcheckSettings = new SpellcheckSettingsWrapper(spellcheckerSettings, null);
    }
  }
}
