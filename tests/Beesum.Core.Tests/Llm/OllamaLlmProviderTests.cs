using System.Net;
using Beesum.Core.Llm;
using Beesum.Core.Models;
using Beesum.Testing;
using Xunit;

namespace Beesum.Core.Tests.Llm;

public class OllamaLlmProviderTests
{
    private const string BaseUrl = "http://localhost:11434";
    private const string Model = "llama3.2";

    private static OllamaLlmProvider CreateProvider(InMemoryHttpMessageHandler handler) =>
        new(new InMemoryHttpClientFactory(handler), BaseUrl, Model);

    private static SummaryRequest CreateRequest() =>
        new(
            new Transcript("src", "en", new[]
            {
                new SubtitleSegment(TimeSpan.Zero, TimeSpan.FromSeconds(2), "Hello world"),
            }),
            PromptStyle.Neutral,
            OutputFormat.PlainText);

    [Fact]
    public async Task ProbeAsync_ModelPresent_ReturnsAvailable()
    {
        const string tags = """{"models":[{"name":"llama3.2:latest"}]}""";
        var handler = new InMemoryHttpMessageHandler().EnqueueJson(tags);

        var availability = await CreateProvider(handler).ProbeAsync(CancellationToken.None);

        Assert.True(availability.IsAvailable);
        Assert.Equal(Model, availability.ModelName);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"{BaseUrl}/api/tags", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task ProbeAsync_ModelMissing_ReturnsUnavailableWithMessage()
    {
        const string tags = """{"models":[{"name":"mistral:latest"}]}""";
        var handler = new InMemoryHttpMessageHandler().EnqueueJson(tags);

        var availability = await CreateProvider(handler).ProbeAsync(CancellationToken.None);

        Assert.False(availability.IsAvailable);
        Assert.Contains("not pulled", availability.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProbeAsync_ServerError_ReturnsUnavailable()
    {
        var handler = new InMemoryHttpMessageHandler().EnqueueJson("{}", statusCode: 500);

        var availability = await CreateProvider(handler).ProbeAsync(CancellationToken.None);

        Assert.False(availability.IsAvailable);
        Assert.Contains("500", availability.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SummarizeAsync_Success_ReturnsContentAndUsesChatEndpoint()
    {
        const string chatResponse = """{"model":"llama3.2","message":{"role":"assistant","content":"Summary here."},"done":true,"prompt_eval_count":10,"eval_count":20}""";
        var handler = new InMemoryHttpMessageHandler().EnqueueJson(chatResponse);

        var result = await CreateProvider(handler).SummarizeAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Summary here.", result.Content);
        Assert.Equal("ollama", result.ProviderId);
        Assert.Equal("llama3.2", result.ModelName);
        Assert.NotNull(result.Usage);
        Assert.Equal(30, result.Usage.TotalTokens);
        Assert.NotNull(result.Duration);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"{BaseUrl}/api/chat", request.RequestUri!.ToString());

        var body = Assert.Single(handler.RequestBodies);
        Assert.Contains("\"model\":\"llama3.2\"", body, StringComparison.Ordinal);
        Assert.Contains("\"stream\":false", body, StringComparison.Ordinal);
        Assert.Contains("\"role\":\"system\"", body, StringComparison.Ordinal);
        Assert.Contains("\"role\":\"user\"", body, StringComparison.Ordinal);
        Assert.Contains("Hello world", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SummarizeAsync_Non200_ReturnsFailureWithStatus()
    {
        var handler = new InMemoryHttpMessageHandler().EnqueueJson("model not found", statusCode: 404);

        var result = await CreateProvider(handler).SummarizeAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ollama", result.ProviderId);
        Assert.Contains("404", result.ErrorMessage, StringComparison.Ordinal);
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
