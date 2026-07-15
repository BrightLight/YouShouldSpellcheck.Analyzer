namespace YouShouldSpellcheck.Analyzer.Test
{
  using System.Collections.Immutable;
  using System.Linq;
  using System.Net;
  using System.Net.Sockets;
  using System.Text;
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.Diagnostics;
  using Microsoft.CodeAnalysis.Text;
  using NUnit.Framework;

  [TestFixture]
  public class LanguageToolIntegrationTests
  {
    private const string Source = "public class Test { public string Text = \"This are wrong.\"; }";

    [Test]
    public void CompilerAnalyzerHasNoVisualStudioThreadingOrRestSharpDependency()
    {
      var referencedAssemblies = typeof(SpellcheckAnalyzerBase).Assembly
        .GetReferencedAssemblies()
        .Select(assembly => assembly.Name)
        .ToArray();

      Assert.That(referencedAssemblies, Does.Not.Contain("RestSharp"));
      Assert.That(referencedAssemblies, Does.Not.Contain("Microsoft.VisualStudio.Threading"));
    }

    [Test]
    public async Task CompilationEndModeReportsLanguageToolGrammarDiagnostic()
    {
      using var server = new OneShotLanguageToolServer();
      var serverTask = server.RespondAsync("""
        {"matches":[{"message":"Use 'is' with a singular subject.","shortMessage":"Agreement error","offset":5,"length":3,"replacements":[{"value":"is"}],"rule":{"id":"SUBJECT_VERB_AGREEMENT","issueType":"grammar","category":{"id":"GRAMMAR"}}}]}
        """);

      var diagnostics = await AnalyzeAsync(CreateSettings(server.Url, LanguageToolExecutionMode.CompilationEnd));
      var request = await serverTask;

      Assert.That(request, Does.Contain("language=en-US"));
      var diagnostic = diagnostics.Single(item => item.Id == SpellcheckAnalyzerBase.LanguageToolGrammarDiagnosticId);
      Assert.That(Source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length), Is.EqualTo("are"));
      Assert.That(diagnostic.Properties["suggestion_1"], Is.EqualTo("is"));
    }

    [Test]
    public async Task LanguageToolUrlDoesNotPerformNetworkChecksUnlessExplicitlyEnabled()
    {
      var diagnostics = await AnalyzeAsync(CreateSettings("http://127.0.0.1:1/v2", LanguageToolExecutionMode.Off));

      Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public async Task ExplicitModeReportsUnavailableServer()
    {
      var listener = new TcpListener(IPAddress.Loopback, 0);
      listener.Start();
      var port = ((IPEndPoint)listener.LocalEndpoint).Port;
      listener.Stop();

      var diagnostics = await AnalyzeAsync(CreateSettings($"http://127.0.0.1:{port}/v2", LanguageToolExecutionMode.CompilationEnd));

      Assert.That(diagnostics.Select(diagnostic => diagnostic.Id), Does.Contain(SpellcheckAnalyzerBase.LanguageToolUnavailableDiagnosticId));
    }

    private static string CreateSettings(string url, LanguageToolExecutionMode mode) => $$"""
      <SpellcheckSettings>
        <StringLiteralLanguages>
          <Language LocalDictionaryLanguage="en_US" LanguageToolLanguage="en-US" />
        </StringLiteralLanguages>
        <LanguageToolUrl>{{url}}</LanguageToolUrl>
        <LanguageToolMode>{{mode}}</LanguageToolMode>
        <LanguageToolTimeoutSeconds>2</LanguageToolTimeoutSeconds>
        <LanguageToolMaxConcurrency>2</LanguageToolMaxConcurrency>
      </SpellcheckSettings>
      """;

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string settings)
    {
      var syntaxTree = CSharpSyntaxTree.ParseText(Source);
      var compilation = CSharpCompilation.Create(
        "LanguageToolTest",
        new[] { syntaxTree },
        new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
      var options = new AnalyzerOptions(ImmutableArray.Create<AdditionalText>(
        new InMemoryAdditionalText("/config/youshouldspellcheck.config.xml", settings)));

      return await compilation
        .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new YouShouldSpellcheckDiagnosticAnalyzer()), options)
        .GetAnalyzerDiagnosticsAsync(CancellationToken.None);
    }

    private sealed class InMemoryAdditionalText : AdditionalText
    {
      private readonly SourceText text;

      public InMemoryAdditionalText(string path, string text)
      {
        this.Path = path;
        this.text = SourceText.From(text);
      }

      public override string Path { get; }

      public override SourceText GetText(CancellationToken cancellationToken = default) => this.text;
    }

    private sealed class OneShotLanguageToolServer : System.IDisposable
    {
      private readonly TcpListener listener = new(IPAddress.Loopback, 0);

      public OneShotLanguageToolServer()
      {
        this.listener.Start();
        this.Url = $"http://127.0.0.1:{((IPEndPoint)this.listener.LocalEndpoint).Port}/v2";
      }

      public string Url { get; }

      public async Task<string> RespondAsync(string json)
      {
        using var client = await this.listener.AcceptTcpClientAsync();
        using var stream = client.GetStream();
        var buffer = new byte[8192];
        var received = new StringBuilder();
        var contentLength = 0;
        while (true)
        {
          var count = await stream.ReadAsync(buffer);
          received.Append(Encoding.ASCII.GetString(buffer, 0, count));
          var request = received.ToString();
          var headerEnd = request.IndexOf("\r\n\r\n", System.StringComparison.Ordinal);
          if (headerEnd >= 0)
          {
            var contentLengthHeader = request.Split("\r\n").FirstOrDefault(line => line.StartsWith("Content-Length:", System.StringComparison.OrdinalIgnoreCase));
            if (contentLengthHeader != null)
            {
              contentLength = int.Parse(contentLengthHeader.Substring(contentLengthHeader.IndexOf(':') + 1).Trim(), System.Globalization.CultureInfo.InvariantCulture);
            }

            if (Encoding.ASCII.GetByteCount(request.Substring(headerEnd + 4)) >= contentLength)
            {
              break;
            }
          }
        }

        var body = Encoding.UTF8.GetBytes(json);
        var headers = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(headers);
        await stream.WriteAsync(body);
        return received.ToString();
      }

      public void Dispose() => this.listener.Stop();
    }
  }
}
