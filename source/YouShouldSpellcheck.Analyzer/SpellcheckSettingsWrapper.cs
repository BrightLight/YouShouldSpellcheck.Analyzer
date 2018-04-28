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

    public string[] DefaultLanguages => this.spellcheckSettings.DefaultLanguages;

    public string[] IdentifierLanguages => this.spellcheckSettings.IdentifierLanguages ?? this.spellcheckSettings.DefaultLanguages;

    public string[] ClassNameLanguages => this.spellcheckSettings.ClassNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public string[] MethodNameLanguages => this.spellcheckSettings.MethodNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public string[] VariableNameLanguages => this.spellcheckSettings.VariableNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public string[] PropertyNameLanguages => this.spellcheckSettings.PropertyNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public string[] EnumNameLanguages => this.spellcheckSettings.EnumNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public string[] EnumMemberNameLanguages => this.spellcheckSettings.EnumMemberNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public string[] EventNameLanguages => this.spellcheckSettings.EventNameLanguages ?? this.spellcheckSettings.IdentifierLanguages;

    public string[] CommentLanguages => this.spellcheckSettings.CommentLanguages;

    public string[] AttributeArgumentLanguages => this.spellcheckSettings.AttributeArgumentLanguages;

    public string[] StringLiteralLanguages => this.spellcheckSettings.StringLiteralLanguages;

    public string[] InspectedAttributes => this.spellcheckSettings.InspectedAttributes;

    public string CustomDictionariesFolder => this.spellcheckSettings.CustomDictionariesFolder;

    public bool CheckAttributeArgument(string attributeName, string argumentName)
    {
      return this.spellcheckSettings.CheckAttributeArgument(attributeName, argumentName);
    }
  }
}