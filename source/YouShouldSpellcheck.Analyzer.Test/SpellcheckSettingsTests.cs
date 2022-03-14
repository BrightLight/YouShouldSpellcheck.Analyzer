
namespace YouShouldSpellcheck.Analyzer.Test
{
  using NUnit.Framework;

  [TestFixture]
  public class SpellcheckSettingsTests
  {
    [Test]
    public void TestCustomDictionariesFolder()
    {
      var relativePath = new SpellcheckSettingsWrapper(new SpellcheckSettings { CustomDictionariesFolder = @"..\test\dic" }, @"C:\config-folder\config.xml");
      var absolutePath = new SpellcheckSettingsWrapper(new SpellcheckSettings { CustomDictionariesFolder = @"C:\my-custom\dic" }, @"C:\config.xml");
      var envPath = new SpellcheckSettingsWrapper(new SpellcheckSettings { CustomDictionariesFolder = @"%SystemRoot%\dic" }, @"C:\config.xml");
      var envPath2 = new SpellcheckSettingsWrapper(new SpellcheckSettings { CustomDictionariesFolder = @"%SystemRoot%\..\test\dic" }, @"C:\config.xml");

      Assert.That(relativePath.CustomDictionariesFolder.ToLower(), Is.EqualTo(@"c:\test\dic"));
      Assert.That(absolutePath.CustomDictionariesFolder.ToLower(), Is.EqualTo(@"c:\my-custom\dic"));
      Assert.That(envPath.CustomDictionariesFolder.ToLower(), Is.EqualTo(@"c:\windows\dic"));
      Assert.That(envPath2.CustomDictionariesFolder.ToLower(), Is.EqualTo(@"c:\test\dic"));
    }
  }
}
