namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Diagnostics;
  using System.IO;
  using System.Text;

  public static class Logger
  {
    private const string logfile = @"c:\temp\YouShouldSpellcheck.log";

    private static StreamWriter logWriter;

    [Conditional("DEBUG")]
    public static void Log(string message)
    {
      if (logWriter == null)
      {
        logWriter = new StreamWriter(logfile, true, Encoding.UTF8);
        logWriter.AutoFlush = true;
      }

      logWriter.WriteLine($"{DateTime.Now} " + message);
    }
  }
}