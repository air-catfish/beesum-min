using System.Globalization;
using System.Text;
using Beesum.Core.Parsing;
using Xunit;

namespace Beesum.Core.Tests.Parsing;

/// <summary>
/// No FsCheck here: deterministic pseudo-random input still catches the "parser
/// throws on a stray arrow" class of bugs, and it costs no dependencies.
/// </summary>
public class VttParserPropertyTests
{
    private static readonly VttParser Parser = new();

    [Fact]
    public void Parse_ArbitraryAscii_NeverThrows()
    {
        var random = new Random(20261109);

        for (var iteration = 0; iteration < 500; iteration++)
        {
            var length = random.Next(0, 300);
            var chars = new char[length];
            for (var i = 0; i < chars.Length; i++)
                chars[i] = (char)random.Next(32, 127);

            var transcript = Parser.Parse(new string(chars), "src");

            Assert.NotNull(transcript);
        }
    }

    [Fact]
    public void Parse_ValidVtt_StartTimesAreMonotonic()
    {
        var builder = new StringBuilder("WEBVTT\n\n");
        var start = TimeSpan.Zero;

        for (var i = 0; i < 50; i++)
        {
            builder
                .Append(Format(start))
                .Append(" --> ")
                .Append(Format(start + TimeSpan.FromSeconds(1)))
                .Append('\n')
                .Append("Line ")
                .Append(i)
                .Append("\n\n");

            start += TimeSpan.FromSeconds(2);
        }

        var segments = Parser.Parse(builder.ToString(), "src").Segments;

        Assert.NotEmpty(segments);
        for (var i = 1; i < segments.Count; i++)
            Assert.True(segments[i - 1].Start <= segments[i].Start);
    }

    [Fact]
    public void Parse_CrlfAndLf_ProduceTheSameText()
    {
        const string body = "WEBVTT\n\n00:00:00.000 --> 00:00:01.000\nHello\n";

        var lf = Parser.Parse(body, "src").ToPlainText();
        var crlf = Parser.Parse(body.Replace("\n", "\r\n", StringComparison.Ordinal), "src").ToPlainText();

        Assert.Equal(lf, crlf);
    }

    private static string Format(TimeSpan value) =>
        value.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
}
