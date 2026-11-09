using Beesum.Core.Models;
using Xunit;

namespace Beesum.Min.Cli.Tests;

public class CliOptionsParserInsightsTests
{
    private const string Url = "https://youtube.com/watch?v=abc";

    [Fact]
    public void Parse_Timestamps_SetsFlag()
    {
        var options = CliOptionsParser.Parse(new[] { Url, "--timestamps" });

        Assert.True(options.Timestamps);
    }

    [Fact]
    public void Parse_MindMap_SetsFormat()
    {
        var options = CliOptionsParser.Parse(new[] { Url, "--format", "mindmap" });

        Assert.Equal(OutputFormat.MindMap, options.Format);
    }
    [Fact]
    public void Parse_ClipAndSaveFlags_AreRead()
    {
        var options = CliOptionsParser.Parse(new[]
        {
            Url,
            "--from", "00:01:02",
            "--to", "1:03",
            "--save-vtt", "clip.vtt",
            "--save-transcript", "clip.md",
            "--no-save-prompt",
        });

        Assert.Equal(new TimeSpan(0, 1, 2), options.From);
        Assert.Equal(new TimeSpan(0, 1, 3), options.To);
        Assert.Equal("clip.vtt", options.SaveVttPath);
        Assert.Equal("clip.md", options.SaveTranscriptPath);
        Assert.True(options.NoSavePrompt);
    }

    [Fact]
    public void Parse_FromAfterTo_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => CliOptionsParser.Parse(new[] { Url, "--from", "00:02:00", "--to", "00:01:00" }));
    }
}
