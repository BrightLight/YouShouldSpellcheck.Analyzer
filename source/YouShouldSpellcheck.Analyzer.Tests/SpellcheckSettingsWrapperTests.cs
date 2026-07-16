namespace YouShouldSpellcheck.Analyzer.Test
{
  using NUnit.Framework;

  /// <summary>
  /// Test class for the <see cref="SpellcheckSettingsWrapper"/>.
  /// </summary>
  [TestFixture]
  public class SpellcheckSettingsWrapperTests
  {
    [Test]
    public void MissingAttributesDefaultsToEmpty()
    {
      var settings = new SpellcheckSettingsWrapper(new SpellcheckSettings(), null);

      Assert.That(settings.Attributes, Is.Empty);
    }

    [Test]
    public void TestCustomDictionariesFolder()
    {
      var relativePath = new SpellcheckSettingsWrapper(new SpellcheckSettings { CustomDictionariesFolder = @"..\test\dictionaries" }, @"C:\config-folder\config.xml");
      var absolutePath = new SpellcheckSettingsWrapper(new SpellcheckSettings { CustomDictionariesFolder = @"C:\my-custom\dictionaries" }, @"C:\config.xml");
      var envPath = new SpellcheckSettingsWrapper(new SpellcheckSettings { CustomDictionariesFolder = @"%SystemRoot%\dictionaries" }, @"C:\config.xml");
      var envPath2 = new SpellcheckSettingsWrapper(new SpellcheckSettings { CustomDictionariesFolder = @"%SystemRoot%\..\test\dictionaries" }, @"C:\config.xml");

      Assert.That(relativePath.CustomDictionariesFolder.ToLower(), Is.EqualTo(@"c:\test\dictionaries"));
      Assert.That(absolutePath.CustomDictionariesFolder.ToLower(), Is.EqualTo(@"c:\my-custom\dictionaries"));
      Assert.That(envPath.CustomDictionariesFolder.ToLower(), Is.EqualTo(@"c:\%systemroot%\dictionaries"));
      Assert.That(envPath2.CustomDictionariesFolder.ToLower(), Is.EqualTo(@"c:\test\dictionaries"));
    }
  }
}
