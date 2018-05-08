namespace YouShouldSpellcheck.Analyzer
{
  using System.Collections.Generic;
  using System.Linq;

  public class DefaultSpellcheckSettings : ISpellcheckSettings
  {
    public IEnumerable<ILanguage> DefaultLanguages => Enumerable.Empty<ILanguage>();

    public IEnumerable<ILanguage> IdentifierLanguages => Enumerable.Empty<ILanguage>();

    public IEnumerable<ILanguage> ClassNameLanguages => Enumerable.Empty<ILanguage>();

    public IEnumerable<ILanguage> MethodNameLanguages => Enumerable.Empty<ILanguage>();

    public IEnumerable<ILanguage> VariableNameLanguages => Enumerable.Empty<ILanguage>();

    public IEnumerable<ILanguage> PropertyNameLanguages => Enumerable.Empty<ILanguage>();

    public IEnumerable<ILanguage> EnumNameLanguages => Enumerable.Empty<ILanguage>();

    public IEnumerable<ILanguage> EnumMemberNameLanguages => Enumerable.Empty<ILanguage>();

    public IEnumerable<ILanguage> EventNameLanguages => Enumerable.Empty<ILanguage>();

    public IEnumerable<ILanguage> CommentLanguages => Enumerable.Empty<ILanguage>();

    public IEnumerable<ILanguage> StringLiteralLanguages => Enumerable.Empty<ILanguage>();

    public IEnumerable<IAttributeProperty> Attributes => Enumerable.Empty<IAttributeProperty>();

    public string CustomDictionariesFolder => string.Empty;

    public string LanguageToolUrl => string.Empty;
  }
}