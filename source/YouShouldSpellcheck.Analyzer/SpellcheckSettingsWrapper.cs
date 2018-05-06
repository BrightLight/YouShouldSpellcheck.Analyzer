namespace YouShouldSpellcheck.Analyzer
{
  using System;

  public class SpellcheckSettingsWrapper : ISpellcheckSettings
  {
    private readonly ISpellcheckSettings spellcheckSettings;

    public SpellcheckSettingsWrapper(ISpellcheckSettings spellcheckSettings)
    {
      if (spellcheckSettings == null)
      {
        throw new ArgumentNullException(nameof(spellcheckSettings));
      }

      this.spellcheckSettings = spellcheckSettings;
    }

    public ILanguage[] DefaultLanguages => this.spellcheckSettings.DefaultLanguages;

    public ILanguage[] IdentifierLanguages => this.spellcheckSettings.IdentifierLanguages ?? this.spellcheckSettings.DefaultLanguages;

    public ILanguage[] ClassNameLanguages => this.spellcheckSettings.ClassNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public ILanguage[] MethodNameLanguages => this.spellcheckSettings.MethodNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public ILanguage[] VariableNameLanguages => this.spellcheckSettings.VariableNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public ILanguage[] PropertyNameLanguages => this.spellcheckSettings.PropertyNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public ILanguage[] EnumNameLanguages => this.spellcheckSettings.EnumNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public ILanguage[] EnumMemberNameLanguages => this.spellcheckSettings.EnumMemberNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public ILanguage[] EventNameLanguages => this.spellcheckSettings.EventNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public ILanguage[] CommentLanguages => this.spellcheckSettings.CommentLanguages;

    public ILanguage[] StringLiteralLanguages => this.spellcheckSettings.StringLiteralLanguages;

    public IAttributeProperty[] Attributes => this.spellcheckSettings.Attributes;

    public string CustomDictionariesFolder => this.spellcheckSettings.CustomDictionariesFolder;

    public string LanguageToolUrl => this.spellcheckSettings.LanguageToolUrl;
  }
}