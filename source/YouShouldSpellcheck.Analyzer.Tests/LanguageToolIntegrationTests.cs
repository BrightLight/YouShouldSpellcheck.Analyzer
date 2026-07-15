namespace YouShouldSpellcheck.Analyzer.Test
{
  using System;
  using System.Net;
  using System.Net.Sockets;
  using System.Text;
  using System.Threading;
  using System.Threading.Tasks;
  using AnalyzerFromTemplate2019.Test;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.Testing;
  using NUnit.Framework;

  [TestFixture]
  [NonParallelizable]
  public class LanguageToolIntegrationTests
  {
    [Test]
    public async Task ConfiguredLanguageToolReportsGrammarDiagnostic()
    {
      const string source = @"
using System;

public sealed class DisplayAttribute : Attribute
{
  public string Name { get; set; } = string.Empty;
}

[Display(Name = ""This are wrong."")]
public class TestClass
{
}";

      var listener = new TcpListener(IPAddress.Loopback, 0);
      listener.Start();
      var serverTask = ServeLanguageToolResponseAsync(listener);

      var previousSettings = AnalyzerContext.SpellcheckSettings;
      var endpoint = (IPEndPoint)listener.LocalEndpoint;
      var language = new Language { LocalDictionaryLanguage = "en_US", LanguageToolLanguage = "en-US" };
      AnalyzerContext.SpellcheckSettings = new SpellcheckSettingsWrapper(
        new SpellcheckSettings
        {
          LanguageToolUrl = $"http://127.0.0.1:{endpoint.Port}/v2/",
          Attributes =
          [
            new AttributeProperty
            {
              AttributeName = "DisplayAttribute",
              PropertyName = "Name",
              Languages = [language]
            }
          ]
        },
        null);

      try
      {
        var mistakeOffset = source.IndexOf("are", StringComparison.Ordinal);
        var sourceBeforeMistake = source[..mistakeOffset];
        var line = sourceBeforeMistake.Split('\n').Length;
        var column = mistakeOffset - sourceBeforeMistake.LastIndexOf('\n');
        var expected = new DiagnosticResult(SpellcheckAnalyzerBase.LanguageToolGrammarDiagnosticId, DiagnosticSeverity.Warning)
          .WithLocation("/0/Test0.cs", line, column);

        await CSharpAnalyzerVerifier<StringLiteralSpellcheckAnalyzer>.VerifyAnalyzerAsync(source, expected);
        await serverTask;
      }
      finally
      {
        AnalyzerContext.SpellcheckSettings = previousSettings;
        listener.Stop();
      }
    }

    private static async Task ServeLanguageToolResponseAsync(TcpListener listener)
    {
      using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      using var client = await listener.AcceptTcpClientAsync(timeout.Token);
      using var stream = client.GetStream();

      var requestBuffer = new byte[4096];
      var request = new StringBuilder();
      while (!request.ToString().Contains("\r\n\r\n"))
      {
        var bytesRead = await stream.ReadAsync(requestBuffer, 0, requestBuffer.Length, timeout.Token);
        if (bytesRead == 0)
        {
          break;
        }

        request.Append(Encoding.ASCII.GetString(requestBuffer, 0, bytesRead));
      }

      const string responseBody = "{\"matches\":[{\"message\":\"Agreement error\",\"shortMessage\":\"\",\"offset\":5,\"length\":3,\"replacements\":[{\"value\":\"is\"}],\"sentence\":\"This are wrong.\",\"rule\":{\"id\":\"SUBJECT_VERB_AGREEMENT\",\"description\":\"Subject-verb agreement\",\"issueType\":\"grammar\",\"category\":{\"id\":\"GRAMMAR\",\"name\":\"Grammar\"}}}]}";
      var responseBodyBytes = Encoding.UTF8.GetBytes(responseBody);
      var responseHeaders = Encoding.ASCII.GetBytes(
        $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {responseBodyBytes.Length}\r\nConnection: close\r\n\r\n");

      await stream.WriteAsync(responseHeaders, 0, responseHeaders.Length, timeout.Token);
      await stream.WriteAsync(responseBodyBytes, 0, responseBodyBytes.Length, timeout.Token);
    }
  }
}
