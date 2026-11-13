using System.Text.Json;
using Beesum.Core.Abstractions;
using Beesum.Core.DependencyInjection;
using Beesum.Core.Models;
using Beesum.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Beesum.Mcp.Tests;

public class McpServerTests
{
    /// <summary>Server with the default transcript-only provider: no summarize tools.</summary>
    private static Task<IReadOnlyList<JsonDocument>> RunAsync(params string[] requests) =>
        RunCoreAsync(withEchoProvider: false, requests);

    /// <summary>Server whose default provider is a fake "echo" LLM: summarize tools are enabled.</summary>
    private static Task<IReadOnlyList<JsonDocument>> RunWithProviderAsync(params string[] requests) =>
        RunCoreAsync(withEchoProvider: true, requests);

    /// <summary>Server with extra service registrations (for example a fake yt-dlp).</summary>
    private static Task<IReadOnlyList<JsonDocument>> RunWithServicesAsync(
        Action<IServiceCollection> configure,
        params string[] requests) =>
        RunCoreAsync(withEchoProvider: false, requests, configure);

    private static async Task<IReadOnlyList<JsonDocument>> RunCoreAsync(
        bool withEchoProvider,
        string[] requests,
        Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddBeesum(options => options.OllamaBaseUrl = null);
        if (withEchoProvider)
            services.AddSingleton<ILlmProvider, EchoProvider>();
        configure?.Invoke(services);

        await using var provider = services.BuildServiceProvider();
        var server = new McpServer(
            provider.GetRequiredService<ISummarizerPipelineFactory>(),
            provider.GetRequiredService<ISubtitleParser>(),
            withEchoProvider ? EchoProvider.ProviderId : "transcript-only");

        var input = new StringReader(string.Join("\n", requests) + "\n");
        var output = new StringWriter();

        await server.RunAsync(input, output, CancellationToken.None);

        return output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToList();
    }

    [Fact]
    public async Task GetTranscript_RejectsNonYouTubeUrls()
    {
        var response = Assert.Single(await RunAsync(
            """{"jsonrpc":"2.0","id":10,"method":"tools/call","params":{"name":"get_transcript","arguments":{"url":"file:///etc/passwd"}}}"""));

        var result = response.RootElement.GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Contains("YouTube", result.GetProperty("content")[0].GetProperty("text").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SummarizeUrl_RejectsNonYouTubeUrls()
    {
        var response = Assert.Single(await RunWithProviderAsync(
            """{"jsonrpc":"2.0","id":8,"method":"tools/call","params":{"name":"summarize_url","arguments":{"url":"file:///etc/passwd"}}}"""));

        var result = response.RootElement.GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Contains("YouTube", result.GetProperty("content")[0].GetProperty("text").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Initialize_ReturnsProtocolAndToolsCapability()
    {
        var response = Assert.Single(await RunAsync(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}"""));

        var result = response.RootElement.GetProperty("result");
        Assert.Equal("2025-06-18", result.GetProperty("protocolVersion").GetString());
        Assert.True(result.GetProperty("capabilities").TryGetProperty("tools", out _));
        Assert.Equal("beesum-min", result.GetProperty("serverInfo").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Initialize_AcceptsUtf8Bom()
    {
        var request = "\uFEFF" +
            """{"jsonrpc":"2.0","id":9,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}""";

        var response = Assert.Single(await RunAsync(request));

        Assert.Equal("2025-06-18", response.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString());
    }

    [Fact]
    public async Task Initialize_WithoutProvider_InstructionsPointToGetTranscript()
    {
        var response = Assert.Single(await RunAsync(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}"""));

        var instructions = response.RootElement.GetProperty("result").GetProperty("instructions").GetString();

        Assert.Contains("get_transcript", instructions, StringComparison.Ordinal);
        Assert.Contains("No LLM provider", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Initialize_WithProvider_InstructionsMentionSummarizeTools()
    {
        var response = Assert.Single(await RunWithProviderAsync(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}"""));

        var instructions = response.RootElement.GetProperty("result").GetProperty("instructions").GetString();

        Assert.Contains("get_transcript", instructions, StringComparison.Ordinal);
        Assert.Contains("summarize_url", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToolsList_WithoutProvider_OnlyGetTranscript()
    {
        var response = Assert.Single(await RunAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}"""));
        var tools = response.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToList();

        var tool = Assert.Single(tools);
        Assert.Equal("get_transcript", tool.GetProperty("name").GetString());
        Assert.True(tool.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
    }

    [Fact]
    public async Task ToolsList_WithProvider_HasUrlAndTranscriptTools()
    {
        var response = Assert.Single(await RunWithProviderAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}"""));
        var tools = response.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToList();

        Assert.Equal(new[] { "summarize_url", "summarize_transcript", "get_transcript" }, tools.Select(t => t.GetProperty("name").GetString()));
        Assert.All(tools, tool => Assert.Equal("object", tool.GetProperty("inputSchema").GetProperty("type").GetString()));
        Assert.All(tools.Take(2), tool => Assert.True(
            tool.GetProperty("inputSchema").GetProperty("properties").TryGetProperty("timestamps", out _)));
        Assert.True(tools[2].GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
    }

    [Theory]
    [InlineData("summarize_url", """{"url":"https://youtu.be/abc"}""")]
    [InlineData("summarize_transcript", """{"text":"Hello"}""")]
    public async Task SummarizeTools_WithoutProvider_ReturnHintToUseGetTranscript(string tool, string arguments)
    {
        var request =
            "{\"jsonrpc\":\"2.0\",\"id\":11,\"method\":\"tools/call\",\"params\":{\"name\":\"" + tool +
            "\",\"arguments\":" + arguments + "}}";

        var response = Assert.Single(await RunAsync(request));

        var result = response.RootElement.GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());
        var text = result.GetProperty("content")[0].GetProperty("text").GetString();
        Assert.Contains("No LLM provider", text, StringComparison.Ordinal);
        Assert.Contains("get_transcript", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SummarizeTranscript_Text_ReturnsContent()
    {
        var response = Assert.Single(await RunWithProviderAsync(
            """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"summarize_transcript","arguments":{"text":"Hello world"}}}"""));

        var result = response.RootElement.GetProperty("result");
        Assert.False(result.TryGetProperty("isError", out _));
        Assert.Equal("Hello world", result.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task SummarizeTranscript_Vtt_ParsesCues()
    {
        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "tools/call",
            @params = new
            {
                name = "summarize_transcript",
                arguments = new
                {
                    text = "WEBVTT\n\n00:00:00.000 --> 00:00:01.000\nHi from VTT\n",
                    input_format = "vtt",
                },
            },
        });

        var response = Assert.Single(await RunWithProviderAsync(request));
        var text = response.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString();

        Assert.Contains("Hi from VTT", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SummarizeTranscript_Timestamps_AreApplied()
    {
        var response = Assert.Single(await RunWithProviderAsync(
            """{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"summarize_transcript","arguments":{"text":"Hello","timestamps":true}}}"""));

        var text = response.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString();

        Assert.Equal("[00:00:00] Hello", text);
    }

    [Fact]
    public async Task SummarizeTranscript_TruncatedReply_AppendsWarning()
    {
        var response = Assert.Single(await RunWithProviderAsync(
            """{"jsonrpc":"2.0","id":12,"method":"tools/call","params":{"name":"summarize_transcript","arguments":{"text":"TRUNCATE-ME please"}}}"""));

        var result = response.RootElement.GetProperty("result");
        Assert.False(result.TryGetProperty("isError", out _));
        var text = result.GetProperty("content")[0].GetProperty("text").GetString();
        Assert.StartsWith("TRUNCATE-ME please", text, StringComparison.Ordinal);
        Assert.Contains("may be incomplete", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTranscript_LangArgument_ReachesYtDlp()
    {
        var fake = new CapturingYtDlp();

        var response = Assert.Single(await RunWithServicesAsync(
            services => services.AddSingleton<IYtDlpService>(fake),
            """{"jsonrpc":"2.0","id":13,"method":"tools/call","params":{"name":"get_transcript","arguments":{"url":"https://youtu.be/abc","lang":"ru, en"}}}"""));

        Assert.NotNull(fake.Languages);
        Assert.Equal(new[] { "ru", "en" }, fake.Languages);
        var result = response.RootElement.GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Contains("captured", result.GetProperty("content")[0].GetProperty("text").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTranscript_WithoutLang_UsesServerDefaults()
    {
        var fake = new CapturingYtDlp();

        await RunWithServicesAsync(
            services => services.AddSingleton<IYtDlpService>(fake),
            """{"jsonrpc":"2.0","id":14,"method":"tools/call","params":{"name":"get_transcript","arguments":{"url":"https://youtu.be/abc"}}}""");

        Assert.True(fake.Called);
        Assert.Null(fake.Languages);
    }

    [Fact]
    public async Task GetTranscript_TooManyLanguages_ReturnsError()
    {
        var response = Assert.Single(await RunAsync(
            """{"jsonrpc":"2.0","id":15,"method":"tools/call","params":{"name":"get_transcript","arguments":{"url":"https://youtu.be/abc","lang":"a,b,c,d,e,f"}}}"""));

        var result = response.RootElement.GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Contains("lang must list", result.GetProperty("content")[0].GetProperty("text").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToolsList_GetTranscript_DeclaresLangProperty()
    {
        var response = Assert.Single(await RunAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}"""));
        var tool = Assert.Single(response.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray());

        Assert.True(tool.GetProperty("inputSchema").GetProperty("properties").TryGetProperty("lang", out _));
    }

    [Fact]
    public async Task SummarizeTranscript_Cyrillic_RoundTrips()
    {
        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 16,
            method = "tools/call",
            @params = new
            {
                name = "summarize_transcript",
                arguments = new { text = "Привет, мир! Это проверка кириллицы." },
            },
        });

        var response = Assert.Single(await RunWithProviderAsync(request));
        var text = response.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString();

        Assert.Equal("Привет, мир! Это проверка кириллицы.", text);
    }

    [Fact]
    public async Task UnknownTool_ReturnsToolError()
    {
        var response = Assert.Single(await RunAsync(
            """{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"nope","arguments":{}}}"""));

        var result = response.RootElement.GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Contains("Unknown tool", result.GetProperty("content")[0].GetProperty("text").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownMethod_ReturnsJsonRpcError()
    {
        var response = Assert.Single(await RunAsync("""{"jsonrpc":"2.0","id":5,"method":"resources/list"}"""));

        var error = response.RootElement.GetProperty("error");
        Assert.Equal(-32601, error.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task MalformedJson_ReturnsParseError()
    {
        var response = Assert.Single(await RunAsync("{"));

        var error = response.RootElement.GetProperty("error");
        Assert.Equal(-32700, error.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Ping_ReturnsEmptyResult()
    {
        var response = Assert.Single(await RunAsync("""{"jsonrpc":"2.0","id":6,"method":"ping"}"""));

        Assert.Equal(JsonValueKind.Object, response.RootElement.GetProperty("result").ValueKind);
    }

    /// <summary>yt-dlp stand-in that records the requested languages and always fails.</summary>
    private sealed class CapturingYtDlp : IYtDlpService
    {
        public bool Called { get; private set; }

        public IReadOnlyList<string>? Languages { get; private set; }

        public Task<VideoInfo?> GetInfoAsync(string url, CancellationToken cancellationToken = default) =>
            Task.FromResult<VideoInfo?>(null);

        public Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> UpdateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<SubtitleDownloadResult> DownloadSubtitlesAsync(
            string url,
            string outputDirectory,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            return Task.FromResult(new SubtitleDownloadResult(false, ErrorMessage: "captured"));
        }

        public Task<SubtitleDownloadResult> DownloadSubtitlesAsync(
            string url,
            string outputDirectory,
            IReadOnlyList<string>? languages,
            CancellationToken cancellationToken = default)
        {
            Languages = languages;
            return DownloadSubtitlesAsync(url, outputDirectory, cancellationToken);
        }
    }

    /// <summary>Fake LLM that returns the transcript text, standing in for a configured provider.</summary>
    private sealed class EchoProvider : ILlmProvider
    {
        public const string ProviderId = "echo";

        public string Id => ProviderId;

        public string DisplayName => "Echo (test)";

        public Task<LlmAvailability> ProbeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LlmAvailability(true));

        public Task<SummaryResult> SummarizeAsync(
            SummaryRequest request,
            CancellationToken cancellationToken = default)
        {
            var text = request.Transcript.ToPlainText(request.Timestamps);
            return Task.FromResult(new SummaryResult(true, text, Id)
            {
                Truncated = text.Contains("TRUNCATE-ME", StringComparison.Ordinal),
            });
        }
    }
}
