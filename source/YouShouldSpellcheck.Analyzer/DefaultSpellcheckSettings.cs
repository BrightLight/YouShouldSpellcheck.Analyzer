namespace YouShouldSpellcheck.Analyzer
{
  using System.Collections.Generic;

  /// <summary>
  /// Default implementation of the <see cref="ISpellcheckSettings"/> interface.
  /// All properties return an empty collection or an empty string.
  /// </summary>
  public class DefaultSpellcheckSettings : ISpellcheckSettings
  {
    /// <inheritdoc />
    public IEnumerable<ILanguage> DefaultLanguages => [];

    /// <inheritdoc />
    public IEnumerable<ILanguage> IdentifierLanguages => [];

    /// <inheritdoc />
    public IEnumerable<ILanguage> ClassNameLanguages => [];

    /// <inheritdoc />
    public IEnumerable<ILanguage> MethodNameLanguages => [];

    /// <inheritdoc />
    public IEnumerable<ILanguage> VariableNameLanguages => [];

    /// <inheritdoc />
    public IEnumerable<ILanguage> PropertyNameLanguages => [];

    /// <inheritdoc />
    public IEnumerable<ILanguage> EnumNameLanguages => [];

    /// <inheritdoc />
    public IEnumerable<ILanguage> EnumMemberNameLanguages => [];

    /// <inheritdoc />
    public IEnumerable<ILanguage> EventNameLanguages => [];

    /// <inheritdoc />
    public IEnumerable<ILanguage> CommentLanguages => [];

    /// <inheritdoc />
    public IEnumerable<ILanguage> StringLiteralLanguages => [];

    /// <inheritdoc />
    public IEnumerable<IAttributeProperty> Attributes => [];

    /// <inheritdoc />
    public string CustomDictionariesFolder => string.Empty;

    /// <inheritdoc />
    public string LanguageToolUrl => string.Empty;
  }
}