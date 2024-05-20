namespace YouShouldSpellcheck.Analyzer.LanguageTool
{
  using System.Collections.Generic;

  public class Match
  {
    public string? Message { get; set; }
    public string? ShortMessage { get; set; }
    public int Offset { get; set; }
    public int Length { get; set; }
    public List<Replacement> Replacements { get; set; } = [];
    public Context? Context { get; set; }
    public string? Sentence { get; set; }
    public Rule? Rule { get; set; }
  }

  public class Replacement
  {
    public string? Value { get; set; }
  }
}
