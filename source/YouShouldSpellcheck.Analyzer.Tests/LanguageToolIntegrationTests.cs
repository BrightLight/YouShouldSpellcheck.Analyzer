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
      Assert.That(request, Does.Contain("text=This+are+wrong."));
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

    [Test]
    public async Task XmlDocumentationIsNotSentToLanguageTool()
    {
      const string source = """
        /// <summary>This are documented incorrectly.</summary>
        public class Test { public string Text = "This are wrong."; }
        """;
      using var server = new OneShotLanguageToolServer();
      var serverTask = server.RespondAsync("""{"matches":[]}""");

      var diagnostics = await AnalyzeAsync(CreateSettings(server.Url, LanguageToolExecutionMode.CompilationEnd), source);
      var request = await serverTask;

      Assert.That(request, Does.Contain("text=This+are+wrong."));
      Assert.That(request, Does.Not.Contain("documented"));
      Assert.That(diagnostics.Select(diagnostic => diagnostic.Id), Does.Not.Contain(SpellcheckAnalyzerBase.LanguageToolUnavailableDiagnosticId));
    }

    [Test]
    public async Task AttributeOnlyScopeDoesNotSendOrdinaryStringLiterals()
    {
      const string source = """
        public sealed class UiAttribute : System.Attribute { public string Text { get; set; } = ""; }
        [Ui(Text = "Attribute UI text.")]
        public class Test { public string Text = "Ordinary UI text."; }
        """;
      const string attributes = """
        <Attributes>
          <AttributeProperty>
            <AttributeName>UiAttribute</AttributeName>
            <PropertyName>Text</PropertyName>
            <Languages><Language LocalDictionaryLanguage="en_US" LanguageToolLanguage="en-US" /></Languages>
          </AttributeProperty>
        </Attributes>
        """;
      using var server = new OneShotLanguageToolServer();
      var serverTask = server.RespondAsync("""{"matches":[]}""");
      var settings = CreateSettings(
        server.Url,
        LanguageToolExecutionMode.CompilationEnd,
        LanguageToolScope.AttributeArgumentsOnly,
        attributes: attributes);

      var diagnostics = await AnalyzeAsync(settings, source);
      var request = await serverTask;

      Assert.That(request, Does.Contain("text=Attribute+UI+text."));
      Assert.That(request, Does.Not.Contain("Ordinary"));
      Assert.That(diagnostics.Select(diagnostic => diagnostic.Id), Does.Not.Contain(SpellcheckAnalyzerBase.LanguageToolUnavailableDiagnosticId));
    }

    [Test]
    public async Task FailedRequestDoesNotDiscardSuccessfulCandidateDiagnostics()
    {
      const string source = "public class Test { string First = \"Server failure.\"; string Second = \"This are wrong.\"; }";
      using var server = new MultiRequestLanguageToolServer();
      var serverTask = server.RespondAsync(2, request =>
        request.Contains("Server+failure.", System.StringComparison.Ordinal)
          ? new TestResponse(500, """{"error":"failure"}""")
          : new TestResponse(200, """{"matches":[{"message":"Agreement error","offset":5,"length":3,"replacements":[],"rule":{"id":"AGREEMENT","issueType":"grammar","category":{"id":"GRAMMAR"}}}]}"""));

      var diagnostics = await AnalyzeAsync(CreateSettings(server.Url, LanguageToolExecutionMode.CompilationEnd), source);
      await serverTask;

      Assert.That(diagnostics.Select(diagnostic => diagnostic.Id), Does.Contain(SpellcheckAnalyzerBase.LanguageToolGrammarDiagnosticId));
      Assert.That(diagnostics.Select(diagnostic => diagnostic.Id), Does.Contain(SpellcheckAnalyzerBase.LanguageToolUnavailableDiagnosticId));
    }

    [Test]
    public async Task FindingIsSuppressedWhenAnotherConfiguredLanguageAcceptsTheSpan()
    {
      const string languages = """
        <Language LocalDictionaryLanguage="en_US" LanguageToolLanguage="en-US" />
        <Language LocalDictionaryLanguage="de_DE" LanguageToolLanguage="de-DE" />
        """;
      using var server = new MultiRequestLanguageToolServer();
      var serverTask = server.RespondAsync(2, request =>
        request.Contains("language=en-US", System.StringComparison.Ordinal)
          ? new TestResponse(200, """{"matches":[{"message":"Agreement error","offset":5,"length":3,"replacements":[],"rule":{"id":"AGREEMENT","issueType":"grammar","category":{"id":"GRAMMAR"}}}]}""")
          : new TestResponse(200, """{"matches":[]}"""));

      var diagnostics = await AnalyzeAsync(CreateSettings(server.Url, LanguageToolExecutionMode.CompilationEnd, languages: languages));
      await serverTask;

      Assert.That(diagnostics.Select(diagnostic => diagnostic.Id), Does.Not.Contain(SpellcheckAnalyzerBase.LanguageToolGrammarDiagnosticId));
    }

    private static string CreateSettings(
      string url,
      LanguageToolExecutionMode mode,
      LanguageToolScope scope = LanguageToolScope.StringLiteralsAndAttributeArguments,
      string languages = null,
      string attributes = null) => $$"""
      <SpellcheckSettings>
        <StringLiteralLanguages>
          {{languages ?? "<Language LocalDictionaryLanguage=\"en_US\" LanguageToolLanguage=\"en-US\" />"}}
        </StringLiteralLanguages>
        {{attributes}}
        <LanguageToolUrl>{{url}}</LanguageToolUrl>
        <LanguageToolMode>{{mode}}</LanguageToolMode>
        <LanguageToolScope>{{scope}}</LanguageToolScope>
        <LanguageToolTimeoutSeconds>2</LanguageToolTimeoutSeconds>
        <LanguageToolMaxConcurrency>1</LanguageToolMaxConcurrency>
      </SpellcheckSettings>
      """;

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string settings, string source = Source)
    {
      var syntaxTree = CSharpSyntaxTree.ParseText(source);
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

    private sealed class TestResponse
    {
      public TestResponse(int statusCode, string body)
      {
        this.StatusCode = statusCode;
        this.Body = body;
      }

      public int StatusCode { get; }

      public string Body { get; }
    }

    private sealed class MultiRequestLanguageToolServer : System.IDisposable
    {
      private readonly TcpListener listener = new(IPAddress.Loopback, 0);

      public MultiRequestLanguageToolServer()
      {
        this.listener.Start();
        this.Url = $"http://127.0.0.1:{((IPEndPoint)this.listener.LocalEndpoint).Port}/v2";
      }

      public string Url { get; }

      public async Task<ImmutableArray<string>> RespondAsync(int requestCount, System.Func<string, TestResponse> createResponse)
      {
        var requests = ImmutableArray.CreateBuilder<string>();
        for (var requestIndex = 0; requestIndex < requestCount; requestIndex++)
        {
          using var client = await this.listener.AcceptTcpClientAsync();
          using var stream = client.GetStream();
          var request = await ReadRequestAsync(stream);
          requests.Add(request);
          var response = createResponse(request);
          var body = Encoding.UTF8.GetBytes(response.Body);
          var reason = response.StatusCode == 200 ? "OK" : "Internal Server Error";
          var headers = Encoding.ASCII.GetBytes($"HTTP/1.1 {response.StatusCode} {reason}\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
          await stream.WriteAsync(headers);
          await stream.WriteAsync(body);
        }

        return requests.ToImmutable();
      }

      public void Dispose() => this.listener.Stop();
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
        var request = await ReadRequestAsync(stream);
        var body = Encoding.UTF8.GetBytes(json);
        var headers = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(headers);
        await stream.WriteAsync(body);
        return request;
      }

      public void Dispose() => this.listener.Stop();
    }

    private static async Task<string> ReadRequestAsync(System.IO.Stream stream)
    {
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

      return received.ToString();
    }
  }
}
