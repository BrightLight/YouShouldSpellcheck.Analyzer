namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Concurrent;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Text;
  using WeCantSpell.Hunspell;

  public static class DictionaryManager
  {
    private static readonly ConcurrentDictionary<string, WordList> dictionaries = new ConcurrentDictionary<string, WordList>();

    private static readonly ConcurrentDictionary<string, List<string>> customWordsByLanguage = new ConcurrentDictionary<string, List<string>>();

    private static readonly ConcurrentDictionary<Tuple<string, string>, bool> cache = new ConcurrentDictionary<Tuple<string, string>, bool>();

    public static bool IsWordCorrect(string word, string language)
    {
      if (string.IsNullOrEmpty(language))
      {
        return true;
      }

      bool wordIsOkay;
      var key = new Tuple<string, string>(language, word);
      if (cache.TryGetValue(key, out wordIsOkay))
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
      wordIsOkay = dictionary.Check(word);
      cache.TryAdd(key, wordIsOkay);
      return wordIsOkay;
    }

    public static bool Suggest(string word, out List<string> suggestions, string language)
    {
      var dictionary = GetDictionaryForLanguage(language);
      suggestions = dictionary.Suggest(word).ToList();
      return true;
    }

    private static string GetCustomDictionaryFileName(string language)
    {
      return Path.Combine(AnalyzerContext.SpellcheckSettings.CustomDictionariesFolder, $"CustomDictionary.{language}.txt");
    }

    private static void AddToInMemoryCustomDictionary(string wordToIgnore, string language)
    {
      var customDictionary = GetInMemoryCustomDictionary(language);
      customDictionary.Add(wordToIgnore);
    }

    private static List<string> GetInMemoryCustomDictionary(string language)
    {
      List<string> customDictionary;
      if (!customWordsByLanguage.TryGetValue(language, out customDictionary))
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

    private static WordList GetDictionaryForLanguage(string language)
    {
      WordList dictionary;
      if (!dictionaries.TryGetValue(language, out dictionary))
      {
        dictionary = CreateDictionary(language);
        dictionaries.TryAdd(language, dictionary);
      }

      return dictionary;
    }

    private static WordList CreateDictionary(string language)
    {
      var dictionariesFolder = AnalyzerContext.SpellcheckSettings.CustomDictionariesFolder;
      var affixFile = Path.Combine(dictionariesFolder, language + ".aff");
      var dictionaryFile = Path.Combine(dictionariesFolder, language + ".dic");
      if (File.Exists(affixFile) && File.Exists(dictionaryFile))
      {
        Logger.Log($"Creating new dictionary instance with dictionary file [{dictionaryFile}] and affix file [{affixFile}]");
        return WordList.CreateFromFiles(dictionaryFile, affixFile);
      }

      Logger.Log($"Dictionary file not found: [{dictionaryFile}]");
      Logger.Log($"Affix file not found: [{affixFile}]");
      return null;
    }
  }
}