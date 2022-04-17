namespace YouShouldSpellcheck.Analyzer.Test
{
  using System;
  using System.IO;
  using TestHelper;

  public class SpellcheckAnalyzerDiagnosticVerifier : DiagnosticVerifier
  {
    protected void SetupSpellcheckerSettings()
    {
      var customDictionariesRootFolder = Environment.GetEnvironmentVariable("APPVEYOR_BUILD_FOLDER")
                                     ?? @"c:\projects\YouShouldSpellcheck.Analyzer";
      var customDictionariesFolder = Path.Combine(customDictionariesRootFolder, "dic");

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
