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
    public Language[] DefaultLanguages { get; set; }

    public Language[] IdentifierLanguages { get; set; }

    public Language[] ClassNameLanguages { get; set; }

    public Language[] MethodNameLanguages { get; set; }

    public Language[] VariableNameLanguages { get; set; }

    public Language[] PropertyNameLanguages { get; set; }

    public Language[] EnumNameLanguages { get; set; }

    public Language[] EnumMemberNameLanguages { get; set; }

    public Language[] EventNameLanguages { get; set; }

    public Language[] CommentLanguages { get; set; }

    public Language[] StringLiteralLanguages { get; set; }

    public AttributeProperty[] Attributes { get; set; }

    public string CustomDictionariesFolder { get; set; }

    public string LanguageToolUrl { get; set; }
  }

  [Serializable]
  public class AttributeProperty
  {
    public string AttributeName { get; set; }
    public string PropertyName { get; set; }
    public Language[] Languages { get; set; }
  }

  [Serializable]
  public class Language : ILanguage
  {
    [XmlAttribute]
    public string LocalDictionaryLanguage { get; set; }

    [XmlAttribute]
    public string LanguageToolLanguage { get; set; }
  }
}