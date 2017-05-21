namespace YouShouldSpellcheck.Analyzer
{
  public static class SpellcheckSettings
  {
    public static string[] DefaultLanguages => new[] { "en_US" };

    public static string[] ClassNameLanguagses => DefaultLanguages;

    public static string[] MethodNameLanguagses => DefaultLanguages;

    public static string[] VariableNameLanguagses => DefaultLanguages;

    public static string[] PropertyNameLanguagses => DefaultLanguages;

    public static string[] CommentLanguagses => DefaultLanguages;

    public static string[] AttributeArgumentLanguages => new[] { "de_DE_frami" };

    public static string[] StringLiteralLanguages => new[] { "de_DE_frami" };
  }
}
