using System.Net;
using Beesum.Core.Llm;
using Beesum.Core.Models;
using Beesum.Testing;
using Xunit;

namespace Beesum.Core.Tests.Llm;

public class AnthropicLlmProviderTests
{
    private const string BaseUrl = "https://api.anthropic.com";
    private const string Model = "claude-sonnet-4-5";
    private const string Key = "sk-ant-test";

    private static AnthropicLlmProvider CreateProvider(
        InMemoryHttpMessageHandler handler,
        string? key = Key) =>
        new(
            new InMemoryHttpClientFactory(handler),
            BaseUrl,
            Model,
            _ => Task.FromResult(key),
            maxTokens: 512);

    private static SummaryRequest CreateRequest() =>
        new(
            new Transcript("src", "en", new[]
            {
                new SubtitleSegment(TimeSpan.Zero, TimeSpan.FromSeconds(2), "Hello world"),
            }),
            PromptStyle.Neutral,
            OutputFormat.PlainText);

    [Fact]
    public async Task ProbeAsync_ModelListed_ReturnsAvailable()
    {
        const string models = """{"data":[{"id":"claude-sonnet-4-5-20250929"}]}""";
        var handler = new InMemoryHttpMessageHandler().EnqueueJson(models);

        var availability = await CreateProvider(handler).ProbeAsync(CancellationToken.None);

        Assert.True(availability.IsAvailable);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(Key, request.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", request.Headers.GetValues("anthropic-version").Single());
    }

    [Fact]
    public async Task ProbeAsync_Unauthorized_ReturnsInvalidKey()
    {
        var handler = new InMemoryHttpMessageHandler().EnqueueJson("{}", (int)HttpStatusCode.Unauthorized);

        var availability = await CreateProvider(handler).ProbeAsync(CancellationToken.None);

        Assert.False(availability.IsAvailable);
        Assert.Contains("invalid API key", availability.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SummarizeAsync_Success_ReturnsMetadata()
    {
        const string body = """
            {"id":"msg_1","type":"message","role":"assistant","model":"claude-sonnet-4-5",
             "content":[{"type":"text","text":"Summary here."}],
             "usage":{"input_tokens":10,"output_tokens":20}}
            """;
        var handler = new InMemoryHttpMessageHandler().EnqueueJson(body);

        var result = await CreateProvider(handler).SummarizeAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Summary here.", result.Content);
        Assert.Equal("anthropic", result.ProviderId);
        Assert.Equal("claude-sonnet-4-5", result.ModelName);
        Assert.Equal(30, result.Usage!.TotalTokens);
        Assert.NotNull(result.Duration);

        var sent = Assert.Single(handler.RequestBodies);
        Assert.Contains("\"model\":\"claude-sonnet-4-5\"", sent, StringComparison.Ordinal);
        Assert.Contains("\"max_tokens\":512", sent, StringComparison.Ordinal);
        Assert.Contains("\"system\":", sent, StringComparison.Ordinal);
        Assert.Contains("Hello world", sent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SummarizeAsync_RateLimited_RetriesAndSucceeds()
    {
        const string body = """
            {"model":"claude-sonnet-4-5","content":[{"type":"text","text":"ok"}],
             "usage":{"input_tokens":1,"output_tokens":1}}
            """;
        var handler = new InMemoryHttpMessageHandler()
            .EnqueueJson("{}", (int)HttpStatusCode.TooManyRequests)
            .EnqueueJson(body);

        var result = await CreateProvider(handler).SummarizeAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Content);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task SummarizeAsync_NoKey_FailsWithoutHttp()
    {
        var handler = new InMemoryHttpMessageHandler();

        var result = await CreateProvider(handler, key: null)
            .SummarizeAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("API key not configured", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SummarizeAsync_InvalidJson_ReturnsFailure()
    {
        var handler = new InMemoryHttpMessageHandler().EnqueueJson("not json");

        var result = await CreateProvider(handler).SummarizeAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task SummarizeAsync_CallerCancellation_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new InMemoryHttpMessageHandler();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateProvider(handler).SummarizeAsync(CreateRequest(), cts.Token));
    }
}
