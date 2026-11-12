using System.Net;
using Beesum.Core.Llm;
using Beesum.Core.Models;
using Beesum.Testing;
using Xunit;

namespace Beesum.Core.Tests.Llm;

public class OpenAiCompatibleLlmProviderTests
{
    private const string BaseUrl = "https://api.example.com";
    private const string Model = "gpt-test";
    private const string Key = "sk-test-123";

    private static OpenAiCompatibleLlmProvider CreateProvider(
        InMemoryHttpMessageHandler handler,
        string? key = Key) =>
        new(
            new InMemoryHttpClientFactory(handler),
            BaseUrl,
            Model,
            _ => Task.FromResult(key));

    private static SummaryRequest CreateRequest() =>
        new(
            new Transcript("src", "en", new[]
            {
                new SubtitleSegment(TimeSpan.Zero, TimeSpan.FromSeconds(2), "Hello world"),
            }),
            PromptStyle.Neutral,
            OutputFormat.PlainText);

    [Fact]
    public async Task ProbeAsync_NoKey_SendsUnauthenticatedRequest()
    {
        const string models = """{"data":[{"id":"gpt-test"}]}""";
        var handler = new InMemoryHttpMessageHandler().EnqueueJson(models);

        var availability = await CreateProvider(handler, key: null).ProbeAsync(CancellationToken.None);

        Assert.True(availability.IsAvailable);
        var request = Assert.Single(handler.Requests);
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task ProbeAsync_ModelPresent_ReturnsAvailable()
    {
        const string models = """{"data":[{"id":"gpt-test"},{"id":"other"}]}""";
        var handler = new InMemoryHttpMessageHandler().EnqueueJson(models);

        var availability = await CreateProvider(handler).ProbeAsync(CancellationToken.None);

        Assert.True(availability.IsAvailable);
        Assert.Equal(Model, availability.ModelName);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"{BaseUrl}/v1/models", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal(Key, request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task ProbeAsync_Unauthorized_ReturnsInvalidKey()
    {
        var handler = new InMemoryHttpMessageHandler().EnqueueJson("{}", statusCode: (int)HttpStatusCode.Unauthorized);

        var availability = await CreateProvider(handler).ProbeAsync(CancellationToken.None);

        Assert.False(availability.IsAvailable);
        Assert.Contains("invalid API key", availability.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProbeAsync_ModelMissing_ReturnsUnavailable()
    {
        const string models = """{"data":[{"id":"other"}]}""";
        var handler = new InMemoryHttpMessageHandler().EnqueueJson(models);

        var availability = await CreateProvider(handler).ProbeAsync(CancellationToken.None);

        Assert.False(availability.IsAvailable);
        Assert.Contains("not listed", availability.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SummarizeAsync_NoKey_SendsUnauthenticatedRequest()
    {
        const string body = """{"model":"gpt-test","choices":[{"message":{"content":"ok"}}]}""";
        var handler = new InMemoryHttpMessageHandler().EnqueueJson(body);

        var result = await CreateProvider(handler, key: null)
            .SummarizeAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var request = Assert.Single(handler.Requests);
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task SummarizeAsync_Success_ReturnsContentAndUsesChatCompletions()
    {
        const string body = """{"model":"gpt-test","choices":[{"message":{"role":"assistant","content":"Summary here."}}],"usage":{"prompt_tokens":5,"completion_tokens":7,"total_tokens":12}}""";
        var handler = new InMemoryHttpMessageHandler().EnqueueJson(body);

        var result = await CreateProvider(handler)
            .SummarizeAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Summary here.", result.Content);
        Assert.Equal("openai-compatible", result.ProviderId);
        Assert.Equal("gpt-test", result.ModelName);
        Assert.NotNull(result.Usage);
        Assert.Equal(12, result.Usage.TotalTokens);
        Assert.NotNull(result.Duration);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"{BaseUrl}/v1/chat/completions", request.RequestUri!.ToString());
        Assert.Equal(Key, request.Headers.Authorization!.Parameter);

        var sent = Assert.Single(handler.RequestBodies);
        Assert.Contains("\"model\":\"gpt-test\"", sent, StringComparison.Ordinal);
        Assert.Contains("\"stream\":false", sent, StringComparison.Ordinal);
        Assert.Contains("\"role\":\"system\"", sent, StringComparison.Ordinal);
        Assert.Contains("Hello world", sent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SummarizeAsync_Unauthorized_ReturnsFailureWith401()
    {
        var handler = new InMemoryHttpMessageHandler().EnqueueJson(
            """{"error":"bad key"}""",
            statusCode: (int)HttpStatusCode.Unauthorized);

        var result = await CreateProvider(handler)
            .SummarizeAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("401", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SummarizeAsync_RateLimited_ReturnsFailureWith429()
    {
        var handler = new InMemoryHttpMessageHandler().EnqueueJson(
            """{"error":"slow down"}""",
            statusCode: (int)HttpStatusCode.TooManyRequests);

        var result = await CreateProvider(handler)
            .SummarizeAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("429", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SummarizeAsync_InvalidJson_ReturnsFailure()
    {
        var handler = new InMemoryHttpMessageHandler().EnqueueJson("not json");

        var result = await CreateProvider(handler)
            .SummarizeAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task SummarizeAsync_HttpTimeout_ReturnsFailure()
    {
        // Simulates HttpClient's own timeout: it surfaces as TaskCanceledException.
        var handler = new InMemoryHttpMessageHandler().EnqueueException(new TaskCanceledException("timeout"));

        var result = await CreateProvider(handler)
            .SummarizeAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Summarize timed out.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("length", true)]
    [InlineData("stop", false)]
    public async Task SummarizeAsync_FinishReason_SetsTruncated(string finishReason, bool expected)
    {
        var body =
            "{\"model\":\"gpt-test\",\"choices\":[{\"finish_reason\":\"" + finishReason + "\"," +
            "\"message\":{\"content\":\"ok\"}}]}";
        var handler = new InMemoryHttpMessageHandler().EnqueueJson(body);

        var result = await CreateProvider(handler)
            .SummarizeAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Truncated);
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
