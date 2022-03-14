namespace YouShouldSpellcheck.Analyzer.LanguageTool
{
  using System;
  using System.Threading.Tasks;
  using RestSharp;

  public class LanguageToolClient
  {
    public static async Task<LanguageToolResponse> Check(Uri localLanguageToolUri, string text, string language)
    {
      // "https://languagetool.org/api/v2"
      var languageTool = new RestClient(localLanguageToolUri);

      // build GET request to check text in specified language
      var request = new RestRequest("check", Method.Get);
      request.AddParameter("text", text);
      request.AddParameter("language", language);

      // execute the request
      var response = await languageTool.ExecuteAsync<LanguageToolResponse>(request);
      //// var content = response.Content; // raw content as string
      return response.Data;
    }
  }
}
