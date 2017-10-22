﻿namespace YouShouldSpellcheck.Analyzer
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
    internal const string SettingsFileName = "youshouldspellcheck.config.xml";

    private static SpellcheckSettings spellcheckSettings;

    private static SpellcheckSettings defaultSettings = new SpellcheckSettings();

    public static SpellcheckSettings SpellcheckSettings
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
    }

    /// <summary>
    /// Initializes the <see cref="spellcheckSettings"/> if they need initialization.
    /// </summary>
    /// <param name="context">The context that will be used to retrieve the <see cref="spellcheckSettings"/>.</param>
    internal static void InitializeSettings(SyntaxNodeAnalysisContext context)
    {
      if (spellcheckSettings == null)
      {
        spellcheckSettings = GetSpellcheckSettings(context.Options, context.CancellationToken) ?? defaultSettings;
      }
    }

    /// <summary>
    /// Gets the <see cref="SpellcheckSettings"/> using the specified <paramref name="options"/> for retrieval.
    /// </summary>
    /// <param name="options">The analyzer options that will be used to find the <see cref="SettingsFileName"/> file.</param>
    /// <param name="cancellationToken">The cancellation token that the operation will observe.</param>
    /// <returns>A <see cref="SpellcheckSettings"/> instance that represents the settings of the specified <paramref name="options"/>.</returns>
    private static SpellcheckSettings GetSpellcheckSettings(AnalyzerOptions options, CancellationToken cancellationToken)
    {
      return GetSpellcheckSettings(options?.AdditionalFiles ?? ImmutableArray.Create<AdditionalText>(), cancellationToken);
    }

    private static SpellcheckSettings GetSpellcheckSettingsFromTextFile(SourceText settingsXml)
    {
      using (var spellcheckSettingsAsTemporaryMemoryStream = new MemoryStream())
      using (var temporaryStreamWriter = new StreamWriter(spellcheckSettingsAsTemporaryMemoryStream))
      {
        settingsXml.Write(temporaryStreamWriter);
        temporaryStreamWriter.Flush();
        spellcheckSettingsAsTemporaryMemoryStream.Position = 0;
        return GetSpellcheckSettings(spellcheckSettingsAsTemporaryMemoryStream);
      }
    }

    private static SpellcheckSettings GetSpellcheckSettings(Stream settingsXml)
    {
      try
      {
        var spellcheckSettingsSerializer = new XmlSerializer(typeof(SpellcheckSettings));
        spellcheckSettings = spellcheckSettingsSerializer.Deserialize(settingsXml) as SpellcheckSettings;
        return spellcheckSettings;
      }
      catch
      {
        return null;
      }
    }

    private static SpellcheckSettings GetSpellcheckSettings(ImmutableArray<AdditionalText> additionalFiles, CancellationToken cancellationToken)
    {
      foreach (var additionalFile in additionalFiles)
      {
        if (Path.GetFileName(additionalFile.Path).ToLowerInvariant() == SettingsFileName)
        {
          var additionalTextContent = additionalFile.GetText(cancellationToken);
          additionalTextContent.Container.TextChanged += SpellcheckSettingsChanged;

          return GetSpellcheckSettingsFromTextFile(additionalTextContent);
        }
      }

      return null;
    }

    private static void SpellcheckSettingsChanged(object sender, TextChangeEventArgs textChangeEventArgs)
    {
      Logger.Log("Settings have changed.");

      // if new settings are invalid, use the previous settings fallback to the previous settings
      defaultSettings = spellcheckSettings;
      spellcheckSettings = null;
    }

    private static void TrySaveXml()
    {
      var foo = new SpellcheckSettings();
      foo.AttributeArgumentLanguages = new[] { "Demo1", "Demo2" };
      foo.ClassNameLanguages = new[] { "Demo1", "Demo2" };
      foo.CommentLanguages = new[] { "Demo1", "Demo2" };
      foo.CustomDictionariesFolder = "Demo1";
      foo.DefaultLanguages = new[] { "Demo1", "Demo2" };
      foo.InspectedAttributes = new[] { "Demo1", "Demo2" };
      foo.MethodNameLanguages = new[] { "Demo1", "Demo2" };
      foo.PropertyNameLanguages = new[] { "Demo1", "Demo2" };
      foo.StringLiteralLanguages = new[] { "Demo1", "Demo2" };
      foo.VariableNameLanguages = new[] { "Demo1", "Demo2" };
      var writer = new StreamWriter(@"c:\temp\settings.xml");
      var spellcheckSettingsSerializer = new XmlSerializer(typeof(SpellcheckSettings));
      spellcheckSettingsSerializer.Serialize(writer, foo);

    }
  }
}
