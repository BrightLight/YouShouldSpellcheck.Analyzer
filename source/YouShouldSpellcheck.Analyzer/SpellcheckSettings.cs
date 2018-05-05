using System;

namespace YouShouldSpellcheck.Analyzer
{
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
  public class SpellcheckSettings : ISpellcheckSettings
  {
    public string[] DefaultLanguages { get; set; }

    public string[] IdentifierLanguages { get; set; }

    public string[] ClassNameLanguages { get; set; }

    public string[] MethodNameLanguages { get; set; }

    public string[] VariableNameLanguages { get; set; }

    public string[] PropertyNameLanguages { get; set; }

    public string[] EnumNameLanguages { get; set; }

    public string[] EnumMemberNameLanguages { get; set; }

    public string[] EventNameLanguages { get; set; }

    public string[] CommentLanguages { get; set; }

    public string[] StringLiteralLanguages { get; set; }

    public AttributePropertyLanguages[] Attributes { get; set; }

    public string CustomDictionariesFolder { get; set; }

    public string LanguageToolUrl { get; set; }
  }

  [Serializable]
  public class AttributePropertyLanguages
  {
    public string AttributeName { get; set; }
    public string PropertyName { get; set; }
    public string[] Languages { get; set; }
  }
}