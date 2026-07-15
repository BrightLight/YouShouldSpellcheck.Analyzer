namespace YouShouldSpellcheck.Analyzer.LanguageTool
{
  using System.Collections.Generic;
  using System.Runtime.Serialization;

  [DataContract]
  internal sealed class LanguageToolResponse
  {
    [DataMember(Name = "matches")]
    public List<LanguageToolMatch> Matches { get; set; } = [];
  }

  [DataContract]
  internal sealed class LanguageToolMatch
  {
    [DataMember(Name = "message")]
    public string? Message { get; set; }

    [DataMember(Name = "shortMessage")]
    public string? ShortMessage { get; set; }

    [DataMember(Name = "offset")]
    public int Offset { get; set; }

    [DataMember(Name = "length")]
    public int Length { get; set; }

    [DataMember(Name = "replacements")]
    public List<LanguageToolReplacement> Replacements { get; set; } = [];

    [DataMember(Name = "rule")]
    public LanguageToolRule? Rule { get; set; }
  }

  [DataContract]
  internal sealed class LanguageToolReplacement
  {
    [DataMember(Name = "value")]
    public string? Value { get; set; }
  }

  [DataContract]
  internal sealed class LanguageToolRule
  {
    [DataMember(Name = "id")]
    public string? Id { get; set; }

    [DataMember(Name = "issueType")]
    public string? IssueType { get; set; }

    [DataMember(Name = "category")]
    public LanguageToolCategory? Category { get; set; }
  }

  [DataContract]
  internal sealed class LanguageToolCategory
  {
    [DataMember(Name = "id")]
    public string? Id { get; set; }
  }
}
