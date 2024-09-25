namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;

  public class SpellcheckSettingsWrapper : ISpellcheckSettings
  {
    private readonly SpellcheckSettings spellcheckSettings;

    public SpellcheckSettingsWrapper(SpellcheckSettings spellcheckSettings, string? settingsPath)
    {
      this.spellcheckSettings = spellcheckSettings ?? throw new ArgumentNullException(nameof(spellcheckSettings));

      this.CustomDictionariesFolder = EvaluateCustomDirectoryFolder(settingsPath, this.spellcheckSettings.CustomDictionariesFolder);
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

    public IEnumerable<IAttributeProperty> Attributes => this.spellcheckSettings.Attributes.Select(x => new AttributePropertyWrapper(x));

    public string? CustomDictionariesFolder { get; }

    public string? LanguageToolUrl => this.spellcheckSettings.LanguageToolUrl;

    private static string? EvaluateCustomDirectoryFolder(string? configFile, string? rawPath)
    {
      if (rawPath == null)
      {
        return null;
      }

      var path = Environment.ExpandEnvironmentVariables(rawPath);
      var basePath = configFile == null ? Path.GetFullPath(".") : Path.GetDirectoryName(configFile);

      string finalPath;
      if (!Path.IsPathRooted(path) || "\\".Equals(Path.GetPathRoot(path)))
      {
        if (path.StartsWith(Path.DirectorySeparatorChar.ToString()))
        {
          finalPath = Path.Combine(Path.GetPathRoot(basePath), path.TrimStart(Path.DirectorySeparatorChar));
        }
        else
        {
          finalPath = Path.Combine(basePath, path);
        }
      }
      else
      {
        finalPath = path;
      }

      // resolves any internal "..\" to get the true full path.
      return Path.GetFullPath(finalPath);
    }
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