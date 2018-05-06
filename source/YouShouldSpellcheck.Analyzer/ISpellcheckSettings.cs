using System;

namespace YouShouldSpellcheck.Analyzer
{
  public interface ISpellcheckSettings
  {
    ILanguage[] DefaultLanguages { get; }

    ILanguage[] IdentifierLanguages { get; }

    ILanguage[] ClassNameLanguages { get; }

    ILanguage[] MethodNameLanguages { get; }

    ILanguage[] VariableNameLanguages { get; }

    ILanguage[] PropertyNameLanguages { get; }

    ILanguage[] EnumNameLanguages { get; }

    ILanguage[] EnumMemberNameLanguages { get; }

    ILanguage[] EventNameLanguages { get; }

    ILanguage[] CommentLanguages { get; }

    ILanguage[] StringLiteralLanguages { get; }

    IAttributeProperty[] Attributes { get; }

    string CustomDictionariesFolder { get; }

    string LanguageToolUrl { get; }
  }

  public interface IAttributeProperty
  {
    string AttributeName { get; }
    string PropertyName { get; }
    ILanguage[] Languages { get; }
  }

  public interface ILanguage
  {
    string LocalDictionaryLanguage { get; }

    string LanguageToolLanguage { get; }
  }
}