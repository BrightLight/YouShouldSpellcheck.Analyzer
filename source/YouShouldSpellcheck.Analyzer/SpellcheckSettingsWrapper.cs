namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Generic;
  using System.Linq;

  public class SpellcheckSettingsWrapper : ISpellcheckSettings
  {
    private readonly SpellcheckSettings spellcheckSettings;

    public SpellcheckSettingsWrapper(SpellcheckSettings spellcheckSettings)
    {
      this.spellcheckSettings = spellcheckSettings ?? throw new ArgumentNullException(nameof(spellcheckSettings));
    }

    public IEnumerable<ILanguage> DefaultLanguages => this.spellcheckSettings.DefaultLanguages;

    public IEnumerable<ILanguage> IdentifierLanguages => this.spellcheckSettings.IdentifierLanguages ?? this.spellcheckSettings.DefaultLanguages;

    public IEnumerable<ILanguage> ClassNameLanguages => this.spellcheckSettings.ClassNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public IEnumerable<ILanguage> MethodNameLanguages => this.spellcheckSettings.MethodNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public IEnumerable<ILanguage> VariableNameLanguages => this.spellcheckSettings.VariableNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public IEnumerable<ILanguage> PropertyNameLanguages => this.spellcheckSettings.PropertyNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public IEnumerable<ILanguage> EnumNameLanguages => this.spellcheckSettings.EnumNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public IEnumerable<ILanguage> EnumMemberNameLanguages => this.spellcheckSettings.EnumMemberNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public IEnumerable<ILanguage> EventNameLanguages => this.spellcheckSettings.EventNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public IEnumerable<ILanguage> CommentLanguages => this.spellcheckSettings.CommentLanguages;

    public IEnumerable<ILanguage> StringLiteralLanguages => this.spellcheckSettings.StringLiteralLanguages;

    public IEnumerable<IAttributeProperty> Attributes => this.spellcheckSettings.Attributes.Select(x => new AttributePropertyWrapper(x));

    public string CustomDictionariesFolder => Environment.ExpandEnvironmentVariables(this.spellcheckSettings.CustomDictionariesFolder);

    public string LanguageToolUrl => this.spellcheckSettings.LanguageToolUrl;
  }

  public class AttributePropertyWrapper : IAttributeProperty
  {
    private readonly AttributeProperty attributeProperty;

    public AttributePropertyWrapper(AttributeProperty attributeProperty)
    {
      this.attributeProperty = attributeProperty;
    }

    public string AttributeName => this.attributeProperty.AttributeName;

    public string PropertyName => this.attributeProperty.PropertyName;

    public IEnumerable<ILanguage> Languages => this.attributeProperty.Languages;
  }
}