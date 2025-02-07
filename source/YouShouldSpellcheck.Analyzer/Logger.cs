﻿namespace YouShouldSpellcheck.Analyzer
{
  using System;
  using System.Diagnostics;
  using System.IO;
  using System.Text;

  public static class Logger
  {
    private const string logfile = @"c:\temp\YouShouldSpellcheck.log";

    [Conditional("DEBUG")]
    public static void Log(string message)
    {
      try
      {
        using (var logWriter = new StreamWriter(logfile, true, Encoding.UTF8))
        {
          logWriter.AutoFlush = true;
          logWriter.WriteLine($"{DateTime.Now} " + message);
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }
    }

    public static void Log(Exception exception)
    {
      Log(exception.ToString());
    }

    public static void Log(LanguageTool.Match match)
    {
      var message = $"Rule [{match.Rule?.Id}] {match.Rule?.Description} (Category [{match.Rule?.Category?.Id}] {match.Rule?.Category?.Name}): {match.ShortMessage}\r\n{match.Message}";
      Log(message);

      if (match.Rule?.Urls != null)
      {
        foreach (var ruleUrl in match.Rule.Urls)
        {
          Log($"Rule-Url: {ruleUrl}");
        }
      }
    }
  }
}