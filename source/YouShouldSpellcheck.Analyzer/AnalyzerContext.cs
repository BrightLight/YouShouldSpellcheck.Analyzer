namespace YouShouldSpellcheck.Analyzer
{
  using System.IO;
  using System.Reflection;

  public static class AnalyzerContext
  {
    public static string AnalyzerDirectory
    {
      get
      {
        var executingAssembly = Assembly.GetExecutingAssembly();
        if (executingAssembly != null)
        {
          return Path.GetDirectoryName(executingAssembly.Location);
        }

        return string.Empty;
      }
    }
  }
}
