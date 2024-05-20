namespace YouShouldSpellcheck.Analyzer.LanguageTool
{
  using System.Collections.Generic;

  public class LanguageToolResponse
    ////  {
    ////  "software": {
    ////    "name": "string",
    ////    "version": "string",
    ////    "buildDate": "string",
    ////    "apiVersion": 0,
    ////    "status": "string",
    ////    "premium": true
    ////  },
    ////  "language": {
    ////    "name": "string",
    ////    "code": "string"
    ////  },
    ////  "matches": [
    ////    {
    ////      "message": "string",
    ////      "shortMessage": "string",
    ////      "offset": 0,
    ////      "length": 0,
    ////      "replacements": [
    ////        {
    ////          "value": "string"
    ////        }
    ////      ],
    ////      "context": {
    ////        "text": "string",
    ////        "offset": 0,
    ////        "length": 0
    ////      },
    ////      "sentence": "string",
    ////      "rule": {
    ////        "id": "string",
    ////        "subId": "string",
    ////        "description": "string",
    ////        "urls": [
    ////          {
    ////            "value": "string"
    ////          }
    ////        ],
    ////        "issueType": "string",
    ////        "category": {
    ////          "id": "string",
    ////          "name": "string"
    ////        }
    ////      }
    ////    }
    ////  ]
    ////}
  {
    public Software? Software { get; set; }
    public Language? Language { get; set; }
    public List<Match> Matches { get; set; } = [];
  }
}