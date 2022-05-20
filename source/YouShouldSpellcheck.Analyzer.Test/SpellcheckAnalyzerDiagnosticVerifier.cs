namespace YouShouldSpellcheck.Analyzer.Test
{
  using System;
  using System.IO;

  public static class SpellcheckAnalyzerDiagnosticVerifier
  {
    public static void SetupSpellcheckerSettings()
    {
      var customDictionariesRootFolder = Environment.GetEnvironmentVariable("APPVEYOR_BUILD_FOLDER")
                                     ?? @"c:\projects\YouShouldSpellcheck.Analyzer";
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
