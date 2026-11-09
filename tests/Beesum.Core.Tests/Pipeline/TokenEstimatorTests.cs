using Beesum.Core.Models;
using Beesum.Core.Pipeline;
using Xunit;

namespace Beesum.Core.Tests.Pipeline;

public class TokenEstimatorTests
{
    [Fact]
    public void Estimate_EmptyText_IsZero()
    {
        Assert.Equal(0, TokenEstimator.Estimate(string.Empty));
        Assert.Equal(0, TokenEstimator.Estimate((string?)null));
    }

    [Fact]
    public void Estimate_GrowsWithText()
    {
        Assert.True(TokenEstimator.Estimate("hello world") > 0);
        Assert.True(TokenEstimator.Estimate(new string('x', 300)) > TokenEstimator.Estimate("x"));
    }

    [Fact]
    public void Estimate_Transcript_MatchesPlainText()
    {
        var transcript = new Transcript("src", "en", new[]
        {
            new SubtitleSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "hello"),
            new SubtitleSegment(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "world"),
        });

        Assert.Equal(
            TokenEstimator.Estimate(transcript.ToPlainText()),
            TokenEstimator.Estimate(transcript));
    }
}
