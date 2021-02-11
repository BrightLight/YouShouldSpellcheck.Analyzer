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
  public class SpellcheckSettings
  {
    public readonly string[] DefaultLanguages = { "en_US" };

    public string[] ClassNameLanguagses => DefaultLanguages;
    public string[] ClassNameLanguages { get; set; }

    public string[] MethodNameLanguagses => DefaultLanguages;
    public string[] MethodNameLanguages { get; set; }

    public string[] VariableNameLanguagses => DefaultLanguages;
    public string[] VariableNameLanguages { get; set; }

    public string[] PropertyNameLanguagses => DefaultLanguages;
    public string[] PropertyNameLanguages { get; set; }

    public string[] CommentLanguages => new[] { "en_US", "de_DE_frami" };

    public string[] AttributeArgumentLanguages => new[] { "de_DE_frami" };

    public string[] StringLiteralLanguages => new[] { "de_DE_frami" };

    public string[] InspectedAttributes => new[] { "SoloProperty", "LayoutGroupDefinition", "Desc" };

    public string CustomDictionariesFolder => @"c:\YouShouldSpellCheckConfig";

    public bool CheckAttributeArgument(string attributeName, string argumentName)
    {
      switch (attributeName)
      {
        case "LayoutGroupDefinition":
          switch (argumentName)
          {
            case "Caption":
            case "Description":
            case "NativeCaption":
            case "NativeDescription":
              return true;
            default:
              return false;
          }
      }

      return true;
    }
  }
}