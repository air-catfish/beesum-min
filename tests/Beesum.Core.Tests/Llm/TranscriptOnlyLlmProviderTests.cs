using Beesum.Core.Llm;
using Beesum.Core.Models;
using Xunit;

namespace Beesum.Core.Tests.Llm;

public class TranscriptOnlyLlmProviderTests
{
    private readonly TranscriptOnlyLlmProvider _provider = new();

    private static SummaryRequest CreateRequest(string text) =>
        new(
            new Transcript("src", "en", new[]
            {
                new SubtitleSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), text),
            }),
            PromptStyle.Neutral,
            OutputFormat.PlainText);

    [Fact]
    public async Task ProbeAsync_AlwaysAvailable()
    {
        var availability = await _provider.ProbeAsync(CancellationToken.None);
        Assert.True(availability.IsAvailable);
    }

    [Fact]
    public async Task SummarizeAsync_ReturnsPlainText()
    {
        var result = await _provider.SummarizeAsync(CreateRequest("Hello world"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello world", result.Content);
        Assert.Equal("transcript-only", result.ProviderId);
    }

    [Fact]
    public async Task SummarizeAsync_EmptyTranscript_ReturnsEmptyString()
    {
        var empty = new SummaryRequest(
            new Transcript("src", "en", Array.Empty<SubtitleSegment>()),
            PromptStyle.Neutral,
            OutputFormat.PlainText);

        var result = await _provider.SummarizeAsync(empty, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, result.Content);
    }
}
