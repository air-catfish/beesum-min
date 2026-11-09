using System.Net;
using Beesum.Core.Llm;
using Beesum.Core.Models;
using Beesum.Testing;
using Xunit;

namespace Beesum.Core.Tests.Llm;

public class GeminiLlmProviderTests
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com";
    private const string Model = "gemini-2.5-flash";
    private const string Key = "AIza-test";

    private static GeminiLlmProvider CreateProvider(
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
        const string models = """{"models":[{"name":"models/gemini-2.5-flash"}]}""";
        var handler = new InMemoryHttpMessageHandler().EnqueueJson(models);

        var availability = await CreateProvider(handler).ProbeAsync(CancellationToken.None);

        Assert.True(availability.IsAvailable);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(Key, request.Headers.GetValues("x-goog-api-key").Single());
        Assert.DoesNotContain(Key, request.RequestUri!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SummarizeAsync_Success_ReturnsMetadata()
    {
        const string body = """
            {"candidates":[{"content":{"parts":[{"text":"Summary here."}],"role":"model"}}],
             "usageMetadata":{"promptTokenCount":5,"candidatesTokenCount":7,"totalTokenCount":12},
             "modelVersion":"gemini-2.5-flash"}
            """;
        var handler = new InMemoryHttpMessageHandler().EnqueueJson(body);

        var result = await CreateProvider(handler).SummarizeAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Summary here.", result.Content);
        Assert.Equal("gemini", result.ProviderId);
        Assert.Equal("gemini-2.5-flash", result.ModelName);
        Assert.Equal(12, result.Usage!.TotalTokens);
        Assert.NotNull(result.Duration);

        var request = Assert.Single(handler.Requests);
        Assert.EndsWith(":generateContent", request.RequestUri!.ToString(), StringComparison.Ordinal);

        var sent = Assert.Single(handler.RequestBodies);
        Assert.Contains("\"system_instruction\":", sent, StringComparison.Ordinal);
        Assert.Contains("\"maxOutputTokens\":512", sent, StringComparison.Ordinal);
        Assert.Contains("Hello world", sent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SummarizeAsync_BlockedPrompt_ReturnsReason()
    {
        const string body = """{"promptFeedback":{"blockReason":"SAFETY"},"candidates":[]}""";
        var handler = new InMemoryHttpMessageHandler().EnqueueJson(body);

        var result = await CreateProvider(handler).SummarizeAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("SAFETY", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SummarizeAsync_RateLimited_RetriesAndSucceeds()
    {
        const string body = """
            {"candidates":[{"content":{"parts":[{"text":"ok"}]}}],
             "usageMetadata":{"promptTokenCount":1,"candidatesTokenCount":1,"totalTokenCount":2}}
            """;
        var handler = new InMemoryHttpMessageHandler()
            .EnqueueJson("{}", (int)HttpStatusCode.TooManyRequests)
            .EnqueueJson(body);

        var result = await CreateProvider(handler).SummarizeAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
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
    public async Task SummarizeAsync_CallerCancellation_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new InMemoryHttpMessageHandler();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateProvider(handler).SummarizeAsync(CreateRequest(), cts.Token));
    }
}
