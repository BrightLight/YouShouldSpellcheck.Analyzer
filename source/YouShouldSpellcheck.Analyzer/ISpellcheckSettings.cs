namespace YouShouldSpellcheck.Analyzer
{
  public interface ISpellcheckSettings
  {
    string[] DefaultLanguages { get; }

    string[] IdentifierLanguages { get; }

    string[] ClassNameLanguages { get; }

    string[] MethodNameLanguages { get; }

    string[] VariableNameLanguages { get; }

    string[] PropertyNameLanguages { get; }

    string[] EnumNameLanguages { get; }

    string[] EnumMemberNameLanguages { get; }

    string[] EventNameLanguages { get; }

    string[] CommentLanguages { get; }

    string[] StringLiteralLanguages { get; }

    AttributePropertyLanguages[] Attributes { get; }

    string CustomDictionariesFolder { get; }

    string LanguageToolUrl { get; }
  }
}