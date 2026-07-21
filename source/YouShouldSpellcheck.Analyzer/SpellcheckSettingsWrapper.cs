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

    public IEnumerable<ILanguage> ClassNameLanguages => this.spellcheckSettings.ClassNameLanguages ?? this.IdentifierLanguages;

    public IEnumerable<ILanguage> MethodNameLanguages => this.spellcheckSettings.MethodNameLanguages ?? this.IdentifierLanguages;

    public IEnumerable<ILanguage> VariableNameLanguages => this.spellcheckSettings.VariableNameLanguages ?? this.IdentifierLanguages;

    public IEnumerable<ILanguage> PropertyNameLanguages => this.spellcheckSettings.PropertyNameLanguages ?? this.IdentifierLanguages;

    public IEnumerable<ILanguage> EnumNameLanguages => this.spellcheckSettings.EnumNameLanguages ?? this.IdentifierLanguages;

    public IEnumerable<ILanguage> EnumMemberNameLanguages => this.spellcheckSettings.EnumMemberNameLanguages ?? this.IdentifierLanguages;

    public IEnumerable<ILanguage> EventNameLanguages => this.spellcheckSettings.EventNameLanguages ?? this.IdentifierLanguages;

    public IEnumerable<ILanguage> CommentLanguages => this.spellcheckSettings.CommentLanguages ?? this.spellcheckSettings.DefaultLanguages;

    public IEnumerable<ILanguage> StringLiteralLanguages => this.spellcheckSettings.StringLiteralLanguages ?? this.spellcheckSettings.DefaultLanguages;

    public IEnumerable<IAttributeProperty> Attributes =>
      this.spellcheckSettings.Attributes?.Select(x => new AttributePropertyWrapper(x)) ?? Enumerable.Empty<IAttributeProperty>();

    public string? LanguageToolUrl => this.spellcheckSettings.LanguageToolUrl;

    public LanguageToolExecutionMode LanguageToolMode => this.spellcheckSettings.LanguageToolMode;

    public LanguageToolScope LanguageToolScope => this.spellcheckSettings.LanguageToolScope;

    public int LanguageToolTimeoutSeconds => Math.Max(1, this.spellcheckSettings.LanguageToolTimeoutSeconds);

    public int LanguageToolMaxConcurrency => Math.Max(1, this.spellcheckSettings.LanguageToolMaxConcurrency);

    public int MaxSuggestionsPerLanguage => Math.Max(0, this.spellcheckSettings.MaxSuggestionsPerLanguage);

    public int MaxSuggestions => Math.Max(0, this.spellcheckSettings.MaxSuggestions);

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

    public AttributeArgumentKind Kind => this.attributeProperty.Kind;

    public IEnumerable<ILanguage> Languages => this.attributeProperty.Languages;
  }
}
