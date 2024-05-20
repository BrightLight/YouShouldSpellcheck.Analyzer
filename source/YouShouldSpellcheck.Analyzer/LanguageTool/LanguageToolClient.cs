namespace YouShouldSpellcheck.Analyzer.LanguageTool
{
  using System;
  using System.Threading.Tasks;
  using RestSharp;

  /// <summary>
  /// Client for the LanguageTool API.
  /// </summary>
  /// <remarks>
  /// The LanguageTool API is a public API that allows you to check text for spelling and grammar mistakes.
  /// This is only used for string literals (and maybe XML comments in the future), because these represent
  /// whole sentences and are more likely to contain grammar mistakes. Obviously, grammar is not an issue
  /// for identifiers, so we don't check those.
  /// </remarks>
  public class LanguageToolClient
  {
    /// <summary>
    /// Checks the specified <paramref name="text"/> for spelling and grammar mistakes.
    /// </summary>
    /// <param name="localLanguageToolUri">The URI of the local LanguageTool server. </param>
    /// <param name="text">The text to check. </param>
    /// <param name="language">The language of the text.</param>
    /// <returns>The response from the LanguageTool server.</returns>
    public static async Task<LanguageToolResponse?> CheckAsync(Uri localLanguageToolUri, string text, string language)
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
      return response?.Data;
    }
  }
}
