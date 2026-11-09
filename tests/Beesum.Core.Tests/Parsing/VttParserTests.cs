using Beesum.Core.Models;
using Beesum.Core.Parsing;
using Xunit;

namespace Beesum.Core.Tests.Parsing;

public class VttParserTests
{
    private const string SourceId = "test-video";
    private readonly VttParser _parser = new();

    [Fact]
    public void Parse_SimpleCue_ReturnsOneSegment()
    {
        const string vtt = """
            WEBVTT

            00:00:00.000 --> 00:00:02.000
            Hello world
            """;

        var transcript = _parser.Parse(vtt, SourceId);

        Assert.Single(transcript.Segments);
        var seg = transcript.Segments[0];
        Assert.Equal("Hello world", seg.Text);
        Assert.Equal(TimeSpan.Zero, seg.Start);
        Assert.Equal(TimeSpan.FromSeconds(2), seg.End);
        Assert.False(seg.IsTimestampMarker);
    }

    [Fact]
    public void Parse_MultipleCues_ReturnsSegmentsInOrder()
    {
        const string vtt = """
            WEBVTT

            00:00:00.000 --> 00:00:02.000
            First

            00:00:02.000 --> 00:00:04.000
            Second

            00:00:04.000 --> 00:00:06.000
            Third
            """;

        var transcript = _parser.Parse(vtt, SourceId);
        var expected = new[] { "First", "Second", "Third" };

        Assert.Equal(expected, transcript.Segments.Select(s => s.Text));
    }

    [Fact]
    public void Parse_RollingWindowYouTube_DeduplicatesContent()
    {
        const string vtt = """
            WEBVTT
            Kind: captions
            Language: en

            00:00:00.400 --> 00:00:03.170
            hello

            00:00:03.170 --> 00:00:03.180
            hello
            world

            00:00:03.180 --> 00:00:06.700
            hello
            world
            this is

            00:00:06.700 --> 00:00:06.710
            world
            this is
            a video

            00:00:06.710 --> 00:00:09.000
            this is
            a video
            """;

        var transcript = _parser.Parse(vtt, SourceId);
        var expected = new[] { "hello", "world", "this is", "a video" };

        Assert.Equal(expected, transcript.Segments.Select(s => s.Text));
    }

    [Fact]
    public void Parse_ShortCuesUnder100ms_AreSkipped()
    {
        const string vtt = """
            WEBVTT

            00:00:00.000 --> 00:00:02.000
            Keep

            00:00:02.000 --> 00:00:02.050
            Drop

            00:00:02.050 --> 00:00:04.000
            Keep2
            """;

        var transcript = _parser.Parse(vtt, SourceId);
        var expected = new[] { "Keep", "Keep2" };

        Assert.Equal(expected, transcript.Segments.Select(s => s.Text));
    }

    [Fact]
    public void Parse_HtmlEntitiesAndInlineTags_AreCleaned()
    {
        const string vtt = """
            WEBVTT

            00:00:00.000 --> 00:00:02.000
            <c.colorE5E5E5>Rock &amp; Roll</c>

            00:00:02.000 --> 00:00:04.000
            <00:00:02.500><v Speaker>Hi &lt;there&gt;</v>
            """;

        var transcript = _parser.Parse(vtt, SourceId);
        var expected = new[] { "Rock & Roll", "Hi <there>" };

        Assert.Equal(expected, transcript.Segments.Select(s => s.Text));
    }

    [Fact]
    public void Parse_InsertsTimestampMarkerEvery15Minutes()
    {
        const string vtt = """
            WEBVTT

            00:00:00.000 --> 00:00:02.000
            Start

            00:20:00.000 --> 00:20:02.000
            Later
            """;

        var transcript = _parser.Parse(vtt, SourceId);
        var markers = transcript.Segments.Where(s => s.IsTimestampMarker).ToArray();

        Assert.Single(markers);
        Assert.Equal("[00:15:00]", markers[0].Text);
        Assert.Equal(TimeSpan.FromMinutes(15), markers[0].Start);
    }

    [Fact]
    public void Parse_MultipleTimestampMarkers_WhenContentSpans30Minutes()
    {
        const string vtt = """
            WEBVTT

            00:00:00.000 --> 00:00:02.000
            Start

            00:40:00.000 --> 00:40:02.000
            Much later
            """;

        var transcript = _parser.Parse(vtt, SourceId);
        var markers = transcript.Segments
            .Where(s => s.IsTimestampMarker)
            .Select(s => s.Text)
            .ToArray();
        var expected = new[] { "[00:15:00]", "[00:30:00]" };

        Assert.Equal(expected, markers);
    }

    [Fact]
    public void Parse_EmptyOrInvalidInput_ReturnsEmptyTranscript()
    {
        Assert.Empty(_parser.Parse(string.Empty, SourceId).Segments);
        Assert.Empty(_parser.Parse("WEBVTT\n\n", SourceId).Segments);
        Assert.Empty(_parser.Parse("not a vtt file at all", SourceId).Segments);
    }
}
