namespace YouShouldSpellcheck.Analyzer.LanguageTool
{
  using System;
  using System.Collections.Generic;
  using System.Net.Http;
  using System.Runtime.Serialization.Json;
  using System.Threading;
  using System.Threading.Tasks;

  internal sealed class LanguageToolClient : IDisposable
  {
    private readonly HttpClient httpClient;
    private readonly Uri checkUri;

    public LanguageToolClient(Uri baseUri, TimeSpan timeout)
    {
      this.httpClient = new HttpClient { Timeout = timeout };
      this.checkUri = new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/check", UriKind.Absolute);
    }

    public async Task<LanguageToolResponse> CheckAsync(string text, string language, CancellationToken cancellationToken)
    {
      using var content = new FormUrlEncodedContent(new Dictionary<string, string>
      {
        ["text"] = text,
        ["language"] = language,
      });
      using var response = await this.httpClient.PostAsync(this.checkUri, content, cancellationToken).ConfigureAwait(false);
      response.EnsureSuccessStatusCode();
      using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
      var serializer = new DataContractJsonSerializer(typeof(LanguageToolResponse));
      return (LanguageToolResponse?)serializer.ReadObject(stream) ?? new LanguageToolResponse();
    }

    public void Dispose() => this.httpClient.Dispose();
  }
}
