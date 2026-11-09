using Beesum.Core.Abstractions;
using Beesum.Core.Llm;
using Beesum.Core.Models;
using Beesum.Testing;
using Xunit;

namespace Beesum.Core.Tests.Llm;

/// <summary>
/// Every provider must satisfy the same handful of promises. If a new provider
/// cannot pass these, it is not ready.
/// </summary>
public abstract class LlmProviderContractTests
{
    protected abstract string ExpectedId { get; }

    protected abstract ILlmProvider CreateProvider(InMemoryHttpMessageHandler handler);

    protected abstract string ProbeResponse { get; }

    protected abstract string SuccessResponse { get; }

    [Fact]
    public void Id_IsStable() =>
        Assert.Equal(ExpectedId, CreateProvider(new InMemoryHttpMessageHandler()).Id);

    [Fact]
    public async Task ProbeAsync_ReturnsAnAvailability()
    {
        var handler = new InMemoryHttpMessageHandler().EnqueueJson(ProbeResponse);

        var availability = await CreateProvider(handler).ProbeAsync(CancellationToken.None);

        Assert.NotNull(availability);
        Assert.True(availability.IsAvailable);
    }

    [Fact]
    public async Task SummarizeAsync_NullRequest_Throws()
    {
        var provider = CreateProvider(new InMemoryHttpMessageHandler());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => provider.SummarizeAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task SummarizeAsync_Success_ReturnsConsistentMetadata()
    {
        var handler = new InMemoryHttpMessageHandler().EnqueueJson(SuccessResponse);

        var result = await CreateProvider(handler).SummarizeAsync(Request(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExpectedId, result.ProviderId);
        Assert.NotEmpty(result.Content);
        Assert.True(result.Duration is null || result.Duration >= TimeSpan.Zero);
    }

    protected static SummaryRequest Request() =>
        new(
            new Transcript("src", "en", new[]
            {
                new SubtitleSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "Hello"),
            }),
            PromptStyle.Neutral,
            OutputFormat.PlainText);
}

public class TranscriptOnlyContractTests : LlmProviderContractTests
{
    protected override string ExpectedId => "transcript-only";
    protected override string ProbeResponse => "{}";
    protected override string SuccessResponse => "{}";

    protected override ILlmProvider CreateProvider(InMemoryHttpMessageHandler handler) =>
        new TranscriptOnlyLlmProvider();
}

public class OllamaContractTests : LlmProviderContractTests
{
    protected override string ExpectedId => "ollama";
    protected override string ProbeResponse => """{"models":[{"name":"llama3.2"}]}""";
    protected override string SuccessResponse =>
        """{"model":"llama3.2","message":{"role":"assistant","content":"Summary."}}""";

    protected override ILlmProvider CreateProvider(InMemoryHttpMessageHandler handler) =>
        new OllamaLlmProvider(new InMemoryHttpClientFactory(handler), "http://localhost:11434", "llama3.2");
}

public class OpenAiContractTests : LlmProviderContractTests
{
    protected override string ExpectedId => "openai-compatible";
    protected override string ProbeResponse => """{"data":[{"id":"gpt-test"}]}""";
    protected override string SuccessResponse =>
        """{"model":"gpt-test","choices":[{"message":{"content":"Summary."}}]}""";

    protected override ILlmProvider CreateProvider(InMemoryHttpMessageHandler handler) =>
        new OpenAiCompatibleLlmProvider(
            new InMemoryHttpClientFactory(handler),
            "https://api.example.com",
            "gpt-test",
            _ => Task.FromResult<string?>("sk-test"));
}

public class AnthropicContractTests : LlmProviderContractTests
{
    protected override string ExpectedId => "anthropic";
    protected override string ProbeResponse => """{"data":[{"id":"claude-sonnet-4-5"}]}""";
    protected override string SuccessResponse =>
        """{"model":"claude-sonnet-4-5","content":[{"type":"text","text":"Summary."}],"usage":{"input_tokens":1,"output_tokens":2}}""";

    protected override ILlmProvider CreateProvider(InMemoryHttpMessageHandler handler) =>
        new AnthropicLlmProvider(
            new InMemoryHttpClientFactory(handler),
            "https://api.anthropic.com",
            "claude-sonnet-4-5",
            _ => Task.FromResult<string?>("sk-ant-test"));
}

public class GeminiContractTests : LlmProviderContractTests
{
    protected override string ExpectedId => "gemini";
    protected override string ProbeResponse => """{"models":[{"name":"models/gemini-2.5-flash"}]}""";
    protected override string SuccessResponse =>
        """{"candidates":[{"content":{"parts":[{"text":"Summary."}]}}],"usageMetadata":{"promptTokenCount":1,"candidatesTokenCount":2,"totalTokenCount":3}}""";

    protected override ILlmProvider CreateProvider(InMemoryHttpMessageHandler handler) =>
        new GeminiLlmProvider(
            new InMemoryHttpClientFactory(handler),
            "https://generativelanguage.googleapis.com",
            "gemini-2.5-flash",
            _ => Task.FromResult<string?>("AIza-test"));
}
