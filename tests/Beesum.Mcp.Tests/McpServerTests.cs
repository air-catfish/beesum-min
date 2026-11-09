using System.Text.Json;
using Beesum.Core.Abstractions;
using Beesum.Core.DependencyInjection;
using Beesum.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Beesum.Mcp.Tests;

public class McpServerTests
{
    private static async Task<IReadOnlyList<JsonDocument>> RunAsync(params string[] requests)
    {
        var services = new ServiceCollection();
        services.AddBeesum(options => options.OllamaBaseUrl = null);

        await using var provider = services.BuildServiceProvider();
        var server = new McpServer(
            provider.GetRequiredService<ISummarizerPipelineFactory>(),
            provider.GetRequiredService<ISubtitleParser>());

        var input = new StringReader(string.Join("\n", requests) + "\n");
        var output = new StringWriter();

        await server.RunAsync(input, output, CancellationToken.None);

        return output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToList();
    }

    [Fact]
    public async Task SummarizeUrl_RejectsNonYouTubeUrls()
    {
        var response = Assert.Single(await RunAsync(
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
    public async Task ToolsList_HasUrlAndTranscriptTools()
    {
        var response = Assert.Single(await RunAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}"""));
        var tools = response.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToList();

        Assert.Equal(new[] { "summarize_url", "summarize_transcript" }, tools.Select(t => t.GetProperty("name").GetString()));
        Assert.All(tools, tool => Assert.Equal("object", tool.GetProperty("inputSchema").GetProperty("type").GetString()));
        Assert.All(tools, tool => Assert.True(
            tool.GetProperty("inputSchema").GetProperty("properties").TryGetProperty("timestamps", out _)));
    }

    [Fact]
    public async Task SummarizeTranscript_Text_ReturnsContent()
    {
        var response = Assert.Single(await RunAsync(
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

        var response = Assert.Single(await RunAsync(request));
        var text = response.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString();

        Assert.Contains("Hi from VTT", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SummarizeTranscript_Timestamps_AreApplied()
    {
        var response = Assert.Single(await RunAsync(
            """{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"summarize_transcript","arguments":{"text":"Hello","timestamps":true}}}"""));

        var text = response.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString();

        Assert.Equal("[00:00:00] Hello", text);
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
}
