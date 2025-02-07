﻿
using System.Linq;

namespace YouShouldSpellcheck.Analyzer
{
  using System.IO;
  using System.Collections.Immutable;
  using System.Threading;
  using System.Xml.Serialization;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.Diagnostics;
  using Microsoft.CodeAnalysis.Text;

  public static class AnalyzerContext
  {
    private const string SettingsFileName = "youshouldspellcheck.config.xml";

    private static ISpellcheckSettings? spellcheckSettings;

    private static ISpellcheckSettings defaultSettings = new DefaultSpellcheckSettings();

    public static ISpellcheckSettings SpellcheckSettings
    {
      get
      {
        ////TrySaveXml();
        if (spellcheckSettings != null)
        {
          return spellcheckSettings;
        }

        return defaultSettings;
      }

      set
      {
        spellcheckSettings = value;
      }
    }

    /// <summary>
    /// Initializes the <see cref="spellcheckSettings"/> if they need initialization.
    /// </summary>
    /// <param name="context">The context that will be used to retrieve the <see cref="spellcheckSettings"/>.</param>
    internal static void InitializeSettings(SyntaxNodeAnalysisContext context)
    {
      RegisterDictionaries(context.Options.AdditionalFiles);
      
      spellcheckSettings ??= GetSpellcheckSettings(context.Options, context.CancellationToken) ?? defaultSettings;
    }

    /// <summary>
    /// look at the additional files. This the ".dic" and ".aff" files with the same name
    /// the filename without extension is the language name
    /// register this language and its ".dic" and ".aff" file with the dictionary manager
    /// using DictionaryManager.RegisterDictionary(language, dic, aff);
    /// </summary>
    /// <param name="additionalFiles"></param>
    private static void RegisterDictionaries(ImmutableArray<AdditionalText> additionalFiles)
    {
      // we get called multiple times, but we only want to do this once
      if (DictionaryManager.IsInitialized)
      {
        return;
      }

      // search dictionaty files (.dic) and their corresponding affix files (.aff)
      // if both are found, register the dictionary
      foreach (var dicFile in additionalFiles.Where(x => Path.GetExtension(x.Path) == ".dic"))
      {
        // try to find corresponding .aff file
        var affFileName = Path.ChangeExtension(dicFile.Path, ".aff");
        var affFile = additionalFiles.FirstOrDefault(x => x.Path == affFileName);

        if (affFile != null)
        {
          var language = Path.GetFileNameWithoutExtension(dicFile.Path);
          var dicSourceText = dicFile.GetText();
          var affSourceText = affFile.GetText();
          if (dicSourceText != null && affSourceText != null)
          {
            DictionaryManager.RegisterDictionary(language, dicSourceText, affSourceText);
          }
        }
      }
    }

    /// <summary>
    /// Gets the <see cref="SpellcheckSettings"/> using the specified <paramref name="options"/> for retrieval.
    /// </summary>
    /// <param name="options">The analyzer options that will be used to find the <see cref="SettingsFileName"/> file.</param>
    /// <param name="cancellationToken">The cancellation token that the operation will observe.</param>
    /// <returns>A <see cref="SpellcheckSettings"/> instance that represents the settings of the specified <paramref name="options"/>.</returns>
    private static ISpellcheckSettings? GetSpellcheckSettings(AnalyzerOptions options, CancellationToken cancellationToken)
    {
      return GetSpellcheckSettings(options?.AdditionalFiles ?? ImmutableArray.Create<AdditionalText>(), cancellationToken);
    }

    private static ISpellcheckSettings? GetSpellcheckSettingsFromTextFile(SourceText settingsXml, string settingsPath)
    {
      using (var spellcheckSettingsAsTemporaryMemoryStream = new MemoryStream())
      using (var temporaryStreamWriter = new StreamWriter(spellcheckSettingsAsTemporaryMemoryStream))
      {
        settingsXml.Write(temporaryStreamWriter);
        temporaryStreamWriter.Flush();
        spellcheckSettingsAsTemporaryMemoryStream.Position = 0;
        return GetSpellcheckSettings(spellcheckSettingsAsTemporaryMemoryStream, settingsPath);
      }
    }

    private static ISpellcheckSettings? GetSpellcheckSettings(Stream settingsXml, string settingsPath)
    {
      try
      {
        var spellcheckSettingsSerializer = new XmlSerializer(typeof(SpellcheckSettings));
        if (spellcheckSettingsSerializer.Deserialize(settingsXml) is SpellcheckSettings deserializedSpellcheckSettings)
        {
          spellcheckSettings = new SpellcheckSettingsWrapper(deserializedSpellcheckSettings, settingsPath);
          return spellcheckSettings;
        }

        return null;
      }
      catch
      {
        return null;
      }
    }

    private static ISpellcheckSettings? GetSpellcheckSettings(ImmutableArray<AdditionalText> additionalFiles, CancellationToken cancellationToken)
    {
      foreach (var additionalFile in additionalFiles)
      {
        if (Path.GetFileName(additionalFile.Path).ToLowerInvariant() == SettingsFileName)
        {
          var additionalTextContent = additionalFile.GetText(cancellationToken);
          if (additionalTextContent == null)
          {
            return null;
          }
          
          additionalTextContent.Container.TextChanged += SpellcheckSettingsChanged;

          return GetSpellcheckSettingsFromTextFile(additionalTextContent, additionalFile.Path);
        }
      }

      return null;
    }

    private static void SpellcheckSettingsChanged(object sender, TextChangeEventArgs textChangeEventArgs)
    {
      Logger.Log("Settings have changed.");

      // if new settings are invalid, use the previous settings fallback to the previous settings
      ////defaultSettings = spellcheckSettings;
      spellcheckSettings = null;
    }

    private static void TrySaveXml()
    {
      var foo = new SpellcheckSettings
      {
        ClassNameLanguages = [new Language { LocalDictionaryLanguage = "en_US", LanguageToolLanguage = "en-US" }],
        CommentLanguages =
        [
          new Language { LocalDictionaryLanguage = "en_US", LanguageToolLanguage = "en-US" },
          new Language { LocalDictionaryLanguage = "de_DE_frami", LanguageToolLanguage = "de-DE" }
        ],
        DefaultLanguages = [new Language { LocalDictionaryLanguage = "en_US", LanguageToolLanguage = "en-US" }],
        MethodNameLanguages = [new Language { LocalDictionaryLanguage = "en_US", LanguageToolLanguage = "en-US" }],
        PropertyNameLanguages = [new Language { LocalDictionaryLanguage = "en_US", LanguageToolLanguage = "en-US" }],
        StringLiteralLanguages =
        [
          new Language { LocalDictionaryLanguage = "en_US", LanguageToolLanguage = "en-US" },
          new Language { LocalDictionaryLanguage = "de_DE_frami", LanguageToolLanguage = "de-DE" }
        ],
        VariableNameLanguages = [new Language { LocalDictionaryLanguage = "en_US", LanguageToolLanguage = "en-US" }],
        Attributes =
        [
          new AttributeProperty
          {
            AttributeName = "Display",
            PropertyName = "Name",
            Languages =
            [
              new Language { LocalDictionaryLanguage = "en_US", LanguageToolLanguage = "en-US" },
              new Language { LocalDictionaryLanguage = "en_UK", LanguageToolLanguage = "en-UK" }
            ],
          },
          new AttributeProperty
          {
            AttributeName = "Display",
            PropertyName = "Description",
            Languages =
            [
              new Language { LocalDictionaryLanguage = "en_US", LanguageToolLanguage = "en-US" },
              new Language { LocalDictionaryLanguage = "de_DE_frami", LanguageToolLanguage = "de-DE" }
            ],
          },
          new AttributeProperty
          {
            AttributeName = "RegularExpression",
            PropertyName = "ErrorMessage",
            Languages = new[]
            {
              new Language { LocalDictionaryLanguage = "en_US", LanguageToolLanguage = "en-US" },
              new Language { LocalDictionaryLanguage = "en_UK", LanguageToolLanguage = "en-UK" }
            },
          }
        ],
        CustomDictionariesFolder = @"c:\temp\customdictionariesfolder"
      };

      var writer = new StreamWriter(@"c:\temp\settings.xml");
      var spellcheckSettingsSerializer = new XmlSerializer(typeof(SpellcheckSettings));
      spellcheckSettingsSerializer.Serialize(writer, foo);
    }
  }
}
