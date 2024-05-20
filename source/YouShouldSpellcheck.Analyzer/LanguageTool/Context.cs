namespace YouShouldSpellcheck.Analyzer.LanguageTool
{
  public class Context
  {
    public string? Text { get; set; }
    public int Offset { get; set; }
    public int Length { get; set; }
  }
}