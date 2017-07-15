namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.IO;

  public static class Logger
  {
    private const string logfile = @"c:\temp\YouShouldSpellcheck.log";

    private static StreamWriter logWriter;

    public static void Log(string message)
    {
      if (logWriter == null)
      {
        logWriter = new StreamWriter(logfile);
        logWriter.AutoFlush = true;
      }

      logWriter.WriteLine($"{DateTime.Now} " + message);
    }
  }
}