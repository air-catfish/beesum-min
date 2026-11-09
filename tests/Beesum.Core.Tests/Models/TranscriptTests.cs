using Beesum.Core.Models;
using Xunit;

namespace Beesum.Core.Tests.Models;

public class TranscriptTests
{
    private static Transcript Sample() => new(
        "abc",
        "en",
        new[]
        {
            new SubtitleSegment(TimeSpan.Zero, TimeSpan.Zero, "[00:15:00]", IsTimestampMarker: true),
            new SubtitleSegment(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(7), "Hello"),
            new SubtitleSegment(TimeSpan.FromSeconds(62), TimeSpan.FromSeconds(64), "World"),
        });

    [Fact]
    public void ToPlainText_WithoutTimestamps_KeepsMarkers()
    {
        var text = Sample().ToPlainText();

        Assert.Contains("[00:15:00]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[00:00:05]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ToPlainText_WithTimestamps_PrefixesCuesAndSkipsMarkers()
    {
        var text = Sample().ToPlainText(timestamps: true);

        Assert.Equal("[00:00:05] Hello\n[00:01:02] World", text);
        Assert.DoesNotContain("00:15:00", text, StringComparison.Ordinal);
    }
    [Fact]
    public void Slice_ReturnsOverlappingCues()
    {
        var clipped = Sample().Slice(TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(63));

        Assert.Equal(new[] { "Hello", "World" }, clipped.Segments.Select(s => s.Text));
    }

    [Fact]
    public void Slice_InvalidRange_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => Sample().Slice(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5)));
    }
}
