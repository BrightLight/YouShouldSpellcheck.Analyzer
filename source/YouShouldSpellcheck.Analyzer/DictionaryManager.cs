namespace YouShouldSpellcheck.Analyzer
{
  using System.Collections.Generic;
  using System.IO;
  using System.Text;
  using NHunspell;

  public static class DictionaryManager
  {
    private static bool NHunspellNativeDllPathIsInitialized;
    private static readonly Dictionary<string, Hunspell> dictionaries = new Dictionary<string, Hunspell>();
    private static List<string> customWords;

    public static bool IsWordCorrect(string word, out List<string> suggestions, string language)
    {
      if (IsCustomWord(word))
      {
        suggestions = null;
        return true;
      }

      var hunspell = GetDictionaryForLanguage(language);
      if (hunspell.Spell(word))
      {
        suggestions = null;
        return true;
      }

      suggestions = hunspell.Suggest(word);
      return false;
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

    private static bool IsCustomWord(string word)
    {
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
      if ((File.Exists(affixFile) && File.Exists(dictionaryFile)))
      {
        if (!NHunspellNativeDllPathIsInitialized)
        {
          NHunspellNativeDllPathIsInitialized = true;
          Hunspell.NativeDllPath = analyzerBasePath;
        }

        return new Hunspell(affixFile, dictionaryFile);
      }

      return null;
    }
  }
}
