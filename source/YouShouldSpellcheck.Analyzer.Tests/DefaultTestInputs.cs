namespace YouShouldSpellcheck.Analyzer.Test
{
  using System.Collections.Generic;
  using System.IO;
  using NUnit.Framework;

  internal static class DefaultTestInputs
  {
    private const string Settings = """
      <?xml version="1.0" encoding="utf-8"?>
      <SpellcheckSettings>
        <DefaultLanguages>
          <Language LocalDictionaryLanguage="en_US" LanguageToolLanguage="en-US" />
        </DefaultLanguages>
        <Attributes>
          <AttributeProperty>
            <AttributeName>DisplayAttribute</AttributeName>
            <PropertyName>Name</PropertyName>
            <Languages>
              <Language LocalDictionaryLanguage="en_US" LanguageToolLanguage="en-US" />
            </Languages>
          </AttributeProperty>
        </Attributes>
      </SpellcheckSettings>
      """;

    public static IEnumerable<(string Filename, string Content)> Get()
    {
      var dictionaryFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "dic");
      yield return ("/config/youshouldspellcheck.config.xml", Settings);
      yield return ("/dic/en_US.dic", File.ReadAllText(Path.Combine(dictionaryFolder, "en_US.dic")));
      yield return ("/dic/en_US.aff", File.ReadAllText(Path.Combine(dictionaryFolder, "en_US.aff")));
    }
  }
}
