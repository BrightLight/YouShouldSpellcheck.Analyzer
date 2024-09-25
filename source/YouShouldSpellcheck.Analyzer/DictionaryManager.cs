using Microsoft.CodeAnalysis.Text;

namespace YouShouldSpellcheck.Analyzer
{
  using Microsoft.CodeAnalysis;
  using System;
  using System.Collections.Concurrent;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Text;
  using WeCantSpell.Hunspell;

  public static class DictionaryManager
  {
    private static readonly ConcurrentDictionary<string, WordList?> dictionaries = new();

    private static readonly ConcurrentDictionary<string, List<string>> customWordsByLanguage = new();

    private static readonly ConcurrentDictionary<Tuple<string, string>, bool> cache = new();
    
    private static readonly ConcurrentDictionary<string, (SourceText DictionarySourceText, SourceText AffixSourceText)> dictionariesFilesPerLanguage = new();
    
    public static void RegisterDictionary(string language, SourceText dictionarySourceText, SourceText affixSourceText)
    {
      dictionariesFilesPerLanguage.TryAdd(language, (dictionarySourceText, affixSourceText));
    }

    public static bool IsInitialized => dictionariesFilesPerLanguage.Any();

    public static bool IsWordCorrect(string word, string language)
    {
      if (string.IsNullOrEmpty(language))
      {
        return true;
      }

      var key = new Tuple<string, string>(language, word);
      if (cache.TryGetValue(key, out var wordIsOkay))
      {
        return wordIsOkay;
      }

      if (IsCustomWord(word, language))
      {
        cache.TryAdd(key, true);
        return true;
      }

      Logger.Log($"IsWordCorrect: [{word}] [{language}]");
      var dictionary = GetDictionaryForLanguage(language);
      wordIsOkay = dictionary?.Check(word) ?? false;
      cache.TryAdd(key, wordIsOkay);
      return wordIsOkay;
    }

    public static bool Suggest(string word, out List<string>? suggestions, string language)
    {
      var dictionary = GetDictionaryForLanguage(language);
      if (dictionary != null)
      {
        suggestions = dictionary.Suggest(word).ToList();
        return true;
      }

      suggestions = null;
      return false;
    }

    private static string GetCustomDictionaryFileName(string language)
    {
      if (string.IsNullOrEmpty(AnalyzerContext.SpellcheckSettings.CustomDictionariesFolder))
      {
        return string.Empty;
      }

      return Path.Combine(AnalyzerContext.SpellcheckSettings.CustomDictionariesFolder, $"CustomDictionary.{language}.txt");
    }

    private static void AddToInMemoryCustomDictionary(string wordToIgnore, string language)
    {
      var customDictionary = GetInMemoryCustomDictionary(language);
      customDictionary.Add(wordToIgnore);
    }

    private static List<string> GetInMemoryCustomDictionary(string language)
    {
      if (!customWordsByLanguage.TryGetValue(language, out var customDictionary))
      {
        customDictionary = new List<string>();
        var customDictionaryPath = GetCustomDictionaryFileName(language);
        if (File.Exists(customDictionaryPath))
        {
          using (var customDictionaryStream = File.Open(customDictionaryPath, FileMode.Open, FileAccess.Read))
          using (var customDictionaryReader = new StreamReader(customDictionaryStream, Encoding.UTF8, true))
          {
            var customDictionaryContent = customDictionaryReader.ReadToEnd();
            customDictionary.AddRange(customDictionaryContent.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
            ////customWords.AddRange(File.ReadAllLines(customDictionaryPath, Encoding.UTF8));
          }
        }

        customWordsByLanguage.TryAdd(language, customDictionary);
      }

      return customDictionary;
    }

    public static void AddToCustomDictionary(string wordToIgnore, string language)
    {
      if (!IsCustomWord(wordToIgnore, language))
      {
        AddToInMemoryCustomDictionary(wordToIgnore, language);
        var customDictionaryPath = GetCustomDictionaryFileName(language);
        try
        {
          using (var customDictionaryStream = File.Open(customDictionaryPath, FileMode.OpenOrCreate, FileAccess.Write))
          using (var customDictionaryWriter = new StreamWriter(customDictionaryStream, Encoding.UTF8))
          {
            foreach (var line in customWordsByLanguage[language])
            {
              customDictionaryWriter.WriteLine(line);
            }

            customDictionaryWriter.Flush();
            customDictionaryWriter.Close();

            ////File.WriteAllLines(customDictionaryPath, customWords, Encoding.UTF8);
          }
        }
        catch (Exception e)
        {
          Logger.Log($"An exception occurred while adding [{wordToIgnore}] to the custom dictionary [{customDictionaryPath}]:\r\n{e}");
        }

        // remove from internal cache
        var key = new Tuple<string, string>(language, wordToIgnore);
        cache.TryUpdate(key, true, false);
      }
    }

    public static bool IsCustomWord(string word, string language)
    {
      Logger.Log($"IsCustomWord ({language}): [{word}]");
      var customDictionary = GetInMemoryCustomDictionary(language);
      return customDictionary != null && customDictionary.Contains(word);
    }

    private static WordList? GetDictionaryForLanguage(string language)
    {
      if (!dictionaries.TryGetValue(language, out var dictionary))
      {
        dictionary = CreateDictionary(language);
        dictionaries.TryAdd(language, dictionary);
      }

      return dictionary;
    }

    private static WordList? CreateDictionary(string language)
    {
      if (dictionariesFilesPerLanguage.TryGetValue(language, out var files))
      {
        var dictionarySourceText = files.DictionarySourceText;
        var affixSourceText = files.AffixSourceText;
        var dictionaryAsStream = GenerateStreamFromSourceText(dictionarySourceText);
        var affixAsStream = GenerateStreamFromSourceText(affixSourceText);
        ////Logger.Log($"Creating new dictionary instance with dictionary file [{dictionaryAdditionalFile.Path}] and affix file [{affixAdditionalFile.Path}]");
        // TODO use async
        return WordList.CreateFromStreams(dictionaryAsStream, affixAsStream);
      }

      return null;
    }

    /// <summary>
    /// Generates a <see cref="Stream"/> from the specified <see cref="SourceText"/>.
    /// </summary>
    /// <param name="sourceText">The <see cref="SourceText"/> that will be used to generate the <see cref="Stream"/>.</param>
    /// <returns>A <see cref="Stream"/> that represents the specified <see cref="SourceText"/>.</returns>
    public static Stream GenerateStreamFromSourceText(SourceText sourceText)
    {
      var stream = new MemoryStream();
      var writer = new StreamWriter(stream, Encoding.UTF8);
      sourceText.Write(writer);
      writer.Flush();
      stream.Position = 0; // Reset the stream position to the beginning
      return stream;
    }
  }
}