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
      var settings = new SpellcheckSettingsWrapper(new SpellcheckSettings());

      Assert.That(settings.Attributes, Is.Empty);
    }

    [Test]
    public void SuggestionLimitsDefaultAndClampToZero()
    {
      var defaults = new SpellcheckSettingsWrapper(new SpellcheckSettings());
      var unlimited = new SpellcheckSettingsWrapper(
        new SpellcheckSettings { MaxSuggestionsPerLanguage = -1, MaxSuggestions = 0 });

      Assert.That(defaults.MaxSuggestionsPerLanguage, Is.EqualTo(5));
      Assert.That(defaults.MaxSuggestions, Is.EqualTo(8));
      Assert.That(unlimited.MaxSuggestionsPerLanguage, Is.Zero);
      Assert.That(unlimited.MaxSuggestions, Is.Zero);
    }

  }
}
