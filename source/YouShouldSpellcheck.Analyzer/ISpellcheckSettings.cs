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

    string[] AttributeArgumentLanguages { get; }

    string[] StringLiteralLanguages { get; }

    string[] InspectedAttributes { get; }

    string CustomDictionariesFolder { get; }

    bool CheckAttributeArgument(string attributeName, string argumentName);
  }
}