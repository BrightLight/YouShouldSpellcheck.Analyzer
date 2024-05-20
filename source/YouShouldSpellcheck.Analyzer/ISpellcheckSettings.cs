using System;

namespace YouShouldSpellcheck.Analyzer
{
  using System.Collections.Generic;

  /// <summary>
  /// Interface for the spellcheck settings.
  /// </summary>
  /// <remarks>
  /// Represents all the supported settings for the spellcheck analyzer.
  /// Allowing the user to specify the languages for different types of identifiers,
  /// as well as a folder for custom dictionaries and a URL for the LanguageTool API.
  /// </remarks>
  public interface ISpellcheckSettings
  {
    IEnumerable<ILanguage> DefaultLanguages { get; }

    IEnumerable<ILanguage> IdentifierLanguages { get; }

    /// <summary>
    /// Gets the valid languages for class names.
    /// </summary>
    IEnumerable<ILanguage> ClassNameLanguages { get; }

    /// <summary>
    /// Gets the valid languages for method names.
    /// </summary>
    IEnumerable<ILanguage> MethodNameLanguages { get; }

    /// <summary>
    /// Gets the valid languages for variable names.
    /// </summary>
    IEnumerable<ILanguage> VariableNameLanguages { get; }

    /// <summary>
    /// Gets the valid languages for property names.
    /// </summary>
    IEnumerable<ILanguage> PropertyNameLanguages { get; }

    /// <summary>
    /// Gets the valid languages for enumeration names.
    /// </summary>
    IEnumerable<ILanguage> EnumNameLanguages { get; }

    /// <summary>
    /// Gets the valid languages for enumeration member names.
    /// </summary>
    IEnumerable<ILanguage> EnumMemberNameLanguages { get; }

    /// <summary>
    /// Gets the valid languages for events.
    /// </summary>
    IEnumerable<ILanguage> EventNameLanguages { get; }

    /// <summary>
    /// Gets the valid languages for XML comments.
    /// </summary>
    IEnumerable<ILanguage> CommentLanguages { get; }

    /// <summary>
    /// Gets the valid languages for string literals.
    /// </summary>
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