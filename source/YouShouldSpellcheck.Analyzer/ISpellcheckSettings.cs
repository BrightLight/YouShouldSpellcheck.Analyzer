using System;

namespace YouShouldSpellcheck.Analyzer
{
  using System.Collections.Generic;

  public interface ISpellcheckSettings
  {
    IEnumerable<ILanguage> DefaultLanguages { get; }

    IEnumerable<ILanguage> IdentifierLanguages { get; }

    IEnumerable<ILanguage> ClassNameLanguages { get; }

    IEnumerable<ILanguage> MethodNameLanguages { get; }

    IEnumerable<ILanguage> VariableNameLanguages { get; }

    IEnumerable<ILanguage> PropertyNameLanguages { get; }

    IEnumerable<ILanguage> EnumNameLanguages { get; }

    IEnumerable<ILanguage> EnumMemberNameLanguages { get; }

    IEnumerable<ILanguage> EventNameLanguages { get; }

    IEnumerable<ILanguage> CommentLanguages { get; }

    IEnumerable<ILanguage> StringLiteralLanguages { get; }

    IEnumerable<IAttributeProperty> Attributes { get; }

    string CustomDictionariesFolder { get; }

    string LanguageToolUrl { get; }
  }

  public interface IAttributeProperty
  {
    string AttributeName { get; }
    string PropertyName { get; }
    IEnumerable<ILanguage> Languages { get; }
  }

  public interface ILanguage
  {
    string LocalDictionaryLanguage { get; }

    string LanguageToolLanguage { get; }
  }
}