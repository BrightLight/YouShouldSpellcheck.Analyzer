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
  public static class SpellcheckSettings
  {
    public static readonly string[] DefaultLanguages = { "en_US" };

    public static string[] ClassNameLanguagses => DefaultLanguages;

    public static string[] MethodNameLanguagses => DefaultLanguages;

    public static string[] VariableNameLanguagses => DefaultLanguages;

    public static string[] PropertyNameLanguagses => DefaultLanguages;

    public static string[] CommentLanguages => new[] { "en_US", "de_DE_frami" };

    public static string[] AttributeArgumentLanguages => new[] { "de_DE_frami" };

    public static string[] StringLiteralLanguages => new[] { "de_DE_frami" };

    public static string[] InspectedAttributes => new[] { "LayoutGroupDefinition" };

    public static bool CheckAttributeArgument(string attributeName, string argumentName)
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