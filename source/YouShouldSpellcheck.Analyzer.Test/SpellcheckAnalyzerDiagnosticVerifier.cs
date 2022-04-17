namespace YouShouldSpellcheck.Analyzer.Test
{
  using System;
  using TestHelper;

  public class SpellcheckAnalyzerDiagnosticVerifier : DiagnosticVerifier
  {
    protected void SetupSpellcheckerSettings()
    {
      var customDictionariesFolder = Environment.GetEnvironmentVariable("APPVEYOR_BUILD_FOLDER")
                                     ?? @"c:\projects\YouShouldSpellcheck.Analyzer\dic\";

      var spellcheckerSettings = new SpellcheckSettings()
      {
        MethodNameLanguages = new Language[] { new Language { LocalDictionaryLanguage = "en_US", LanguageToolLanguage = "en-us" } },
        ClassNameLanguages = new Language[] { new Language { LocalDictionaryLanguage = "en_US", LanguageToolLanguage = "en-us" } },
        StringLiteralLanguages = new Language[] { new Language { LocalDictionaryLanguage = "en_US", LanguageToolLanguage = "en-us" } },
        CustomDictionariesFolder = customDictionariesFolder,
      };

      AnalyzerContext.SpellcheckSettings = new SpellcheckSettingsWrapper(spellcheckerSettings, null);
    }
  }
}
