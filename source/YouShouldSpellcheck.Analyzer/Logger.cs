namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Diagnostics;

  public static class Logger
  {
    [Conditional("DEBUG")]
    public static void Log(string message)
    {
      Debug.WriteLine(message);
    }

    [Conditional("DEBUG")]
    public static void Log(Exception exception)
    {
      Debug.WriteLine(exception);
    }
  }
}
