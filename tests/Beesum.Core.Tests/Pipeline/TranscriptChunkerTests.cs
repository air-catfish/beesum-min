using Beesum.Core.Models;
using Beesum.Core.Pipeline;
using Xunit;

namespace Beesum.Core.Tests.Pipeline;

public class TranscriptChunkerTests
{
    private static Transcript TranscriptOf(params string[] lines) =>
        new(
            "src",
            "en",
            lines.Select((line, index) => new SubtitleSegment(
                TimeSpan.FromSeconds(index * 2),
                TimeSpan.FromSeconds(index * 2 + 1),
                line)).ToArray());

    [Fact]
    public void Split_ShortTranscript_ReturnsSingleChunk()
    {
        var transcript = TranscriptOf("one", "two", "three");

        var chunks = TranscriptChunker.Split(transcript, maxTokens: 1000);

        var chunk = Assert.Single(chunks);
        Assert.Equal(transcript.ToPlainText(), chunk.ToPlainText());
        Assert.Equal("src", chunk.SourceId);
        Assert.Equal("en", chunk.Language);
    }

    [Fact]
    public void Split_LongTranscript_KeepsOrderAndText()
    {
        var lines = Enumerable.Range(1, 20)
            .Select(i => $"line {i} with a few words")
            .ToArray();
        var transcript = TranscriptOf(lines);

        var chunks = TranscriptChunker.Split(transcript, maxTokens: 15);

        Assert.True(chunks.Count > 1);
        var combined = string.Join("\n", chunks.Select(c => c.ToPlainText()));
        Assert.Equal(transcript.ToPlainText(), combined);
    }

    [Fact]
    public void Split_OversizedSegment_CutsOnWords()
    {
        var text = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"word{i}"));
        var transcript = TranscriptOf(text);

        var chunks = TranscriptChunker.Split(transcript, maxTokens: 20);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.True(TokenEstimator.Estimate(chunk) <= 20));
        Assert.Equal(text, string.Join(" ", chunks.Select(c => c.ToPlainText())));
    }

    [Fact]
    public void Split_EmptyTranscript_ReturnsItself()
    {
        var transcript = new Transcript("src", "en", Array.Empty<SubtitleSegment>());

        var chunk = Assert.Single(TranscriptChunker.Split(transcript, maxTokens: 10));

        Assert.Empty(chunk.Segments);
    }

    [Fact]
    public void Split_RejectsNonPositiveBudget()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TranscriptChunker.Split(TranscriptOf("x"), maxTokens: 0));
    }
}
