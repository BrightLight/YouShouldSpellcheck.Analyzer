namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Xml.Serialization;

  // TODO: analyzer should be able to allow configuration for separate types of nodes
  // and allow a configuration (on/off) and valid languages/dictionaries per node type:
  // - attribute argument (maybe attribute-specific, e.g. ResourceNames?)
  // - string (e.g. as constant somewhere)
  // - variable name
  // - method name
  // - class name
  // - argument name
  // - allow default language(s) (used if not specified otherwise on node type level)
  // - coming later: grammar check!
  [Serializable]
  public class SpellcheckSettings
  {
    public Language[] DefaultLanguages { get; init; }

    public Language[] IdentifierLanguages { get; init; }

    public Language[] ClassNameLanguages { get; init; }

    public Language[] MethodNameLanguages { get; init; }

    public Language[] VariableNameLanguages { get; init; }

    public Language[] PropertyNameLanguages { get; init; }

    public Language[] EnumNameLanguages { get; init; }

    public Language[] EnumMemberNameLanguages { get; init; }

    public Language[] EventNameLanguages { get; init; }

    public Language[] CommentLanguages { get; init; }

    public Language[] StringLiteralLanguages { get; init; }

    public AttributeProperty[] Attributes { get; init; }

    public string CustomDictionariesFolder { get; init; }

    public string LanguageToolUrl { get; init; }
  }

  [Serializable]
  public class AttributeProperty
  {
    public string AttributeName { get; init; }
    public string PropertyName { get; init; }
    public Language[] Languages { get; init; }
  }

  [Serializable]
  public class Language : ILanguage
  {
    [XmlAttribute]
    public string LocalDictionaryLanguage { get; init; }

    [XmlAttribute]
    public string LanguageToolLanguage { get; init; }
  }
}