namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Text;
  using NHunspell;

  public static class DictionaryManager
  {
    private static readonly Dictionary<string, Hunspell> dictionaries;

    private static List<string> customWords;

    private static readonly Dictionary<Tuple<string, string>, bool> cache = new Dictionary<Tuple<string, string>, bool>();

    static DictionaryManager()
    {
      var analyzerBasePath = AnalyzerContext.AnalyzerDirectory;
      Logger.Log($"AnalyzerBasePath: [{analyzerBasePath}]");
      Hunspell.NativeDllPath = analyzerBasePath;
      Logger.Log("Hunspell.NativeDllPath was set");

      dictionaries = new Dictionary<string, Hunspell>();
    }

    public static bool IsWordCorrect(string word, string language)
    {
      bool wordIsOkay;
      var key = new Tuple<string, string>(language, word);
      if (cache.TryGetValue(key, out wordIsOkay))
      {
        return wordIsOkay;
      }

      if (IsCustomWord(word))
      {
        cache.Add(key, true);
        return true;
      }

      Logger.Log($"IsWordCorrect: [{word}] [{language}]");
      var hunspell = GetDictionaryForLanguage(language);
      wordIsOkay = hunspell.Spell(word);
      cache.Add(key, wordIsOkay);
      return wordIsOkay;
    }

    public static bool Suggest(string word, out List<string> suggestions, string language)
    {
      var hunspell = GetDictionaryForLanguage(language);
      suggestions = hunspell.Suggest(word);
      return true;
    }

    public static void AddToCustomDictionary(string wordToIgnore)
    {
      if (!IsCustomWord(wordToIgnore))
      {
        customWords.Add(wordToIgnore);
        var customDictionaryPath = Path.Combine(AnalyzerContext.AnalyzerDirectory, "CustomWords.txt");
        File.WriteAllLines(customDictionaryPath, customWords, Encoding.UTF8);
      }
    }

    public static bool IsCustomWord(string word)
    {
      Logger.Log($"IsCustomWord: [{word}]");
      if (customWords == null)
      {
        customWords = new List<string>();
        var customDictionaryPath = Path.Combine(AnalyzerContext.AnalyzerDirectory, "CustomWords.txt");
        if (File.Exists(customDictionaryPath))
        {
          customWords.AddRange(File.ReadAllLines(customDictionaryPath, Encoding.UTF8));
        }
      }

      return customWords.Contains(word);
    }

    private static Hunspell GetDictionaryForLanguage(string language)
    {
      Hunspell hunspell;
      if (!dictionaries.TryGetValue(language, out hunspell))
      {
        hunspell = CreateHunspell(language);
        dictionaries.Add(language, hunspell);
      }

      return hunspell;
    }

    private static Hunspell CreateHunspell(string language)
    {
      var analyzerBasePath = AnalyzerContext.AnalyzerDirectory;
      var affixFile = Path.Combine(analyzerBasePath, "dic", language + ".aff");
      var dictionaryFile = Path.Combine(analyzerBasePath, "dic", language + ".dic");
      if (File.Exists(affixFile) && File.Exists(dictionaryFile))
      {
        Logger.Log($"Creating new Hunspell instance with dictionary file [{dictionaryFile}] and affix file [{affixFile}]");
        return new Hunspell(affixFile, dictionaryFile);
      }

      Logger.Log($"Dictionary file not found: [{dictionaryFile}]");
      Logger.Log($"Affix file not found: [{affixFile}]");
      return null;
    }
  }
}
