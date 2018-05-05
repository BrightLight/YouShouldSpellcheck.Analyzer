namespace YouShouldSpellcheck.Analyzer.LanguageTool
{
  using System.Collections.Generic;

  public class Rule
  {
    public string Id { get; set; }
    public string SubId { get; set; }
    public string Description { get; set; }
    public List<string> Urls { get; set; }
    public string IssueType { get; set; }
    public Category Category { get; set; }
  }
}