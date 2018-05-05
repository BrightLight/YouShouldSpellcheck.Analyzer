namespace YouShouldSpellcheck.Analyzer.LanguageTool
{
  using System;
  using RestSharp;

  public class LanguageToolClient
  {
    public static LanguageToolResponse Check(Uri localLanguageToolUri, string text, string language)
    {
      // "https://languagetool.org/api/v2"
      var languageTool = new RestClient(localLanguageToolUri);

      // build GET request to check text in specified language
      var request = new RestRequest("check", Method.GET);
      request.AddParameter("text", text);
      request.AddParameter("language", language);

      // execute the request
      var response = languageTool.Execute<LanguageToolResponse>(request);
      //// var content = response.Content; // raw content as string
      return response.Data;
    }
  }
}
