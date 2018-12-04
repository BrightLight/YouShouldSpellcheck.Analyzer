
namespace YouShouldSpellcheck.Analyzer.Test
{
  using Microsoft.VisualStudio.TestTools.UnitTesting;

  [TestClass]
  public class SpellcheckSettingsTests
  {
    [TestMethod]
    public void TestCustomDictionariesFolder()
    {
      var relativePath = new SpellcheckSettingsWrapper(new SpellcheckSettings { CustomDictionariesFolder = @"..\test\dic" }, @"C:\config-folder\config.xml");
      var absolutePath = new SpellcheckSettingsWrapper(new SpellcheckSettings { CustomDictionariesFolder = @"C:\my-custom\dic" }, @"C:\config.xml");
      var envPath = new SpellcheckSettingsWrapper(new SpellcheckSettings { CustomDictionariesFolder = @"%SystemRoot%\dic" }, @"C:\config.xml");
      var envPath2 = new SpellcheckSettingsWrapper(new SpellcheckSettings { CustomDictionariesFolder = @"%SystemRoot%\..\test\dic" }, @"C:\config.xml");

      Assert.AreEqual(@"c:\test\dic", relativePath.CustomDictionariesFolder.ToLower());
      Assert.AreEqual(@"c:\my-custom\dic", absolutePath.CustomDictionariesFolder.ToLower());
      Assert.AreEqual(@"c:\windows\dic", envPath.CustomDictionariesFolder.ToLower());
      Assert.AreEqual(@"c:\test\dic", envPath2.CustomDictionariesFolder.ToLower());
    }
  }
}
