namespace YouShouldSpellcheck.Analyzer.Test
{
  using System.Collections.Generic;
  using System.IO;
  using NUnit.Framework;

  internal static class DefaultTestInputs
  {
    public const string GlobalConfig = """
      is_global = true
      build_property.YouShouldSpellcheckDefaultLanguagesEncoded = en-US
      build_property.YouShouldSpellcheckDictionaryMappingsEncoded = en-US=en_US
      build_property.YouShouldSpellcheckAttributeArgumentsEncoded = System.ComponentModel.DataAnnotations.DisplayAttribute~Name~~en-US
      """;

    public static IEnumerable<(string Filename, string Content)> Get()
    {
      var dictionaryFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "dictionaries");
      yield return ("/dictionaries/en_US.dic", File.ReadAllText(Path.Combine(dictionaryFolder, "en_US.dic")));
      yield return ("/dictionaries/en_US.aff", File.ReadAllText(Path.Combine(dictionaryFolder, "en_US.aff")));
    }
  }
}
