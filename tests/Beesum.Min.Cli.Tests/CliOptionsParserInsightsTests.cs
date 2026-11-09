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
}
