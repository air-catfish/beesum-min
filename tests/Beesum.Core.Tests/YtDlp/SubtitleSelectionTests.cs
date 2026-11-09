using Beesum.Core.YtDlp;
using Xunit;

namespace Beesum.Core.Tests.YtDlp;

public class SubtitleSelectionTests
{
    private static readonly string[] Files =
    {
        "/tmp/abc.en.vtt",
        "/tmp/abc.ru.vtt",
        "/tmp/abc.en-US.vtt",
    };

    [Fact]
    public void SelectSubtitleFile_UsesRequestedOrder()
    {
        Assert.Equal("/tmp/abc.ru.vtt", YtDlpService.SelectSubtitleFile(Files, new[] { "ru", "en" }));
        Assert.Equal("/tmp/abc.en.vtt", YtDlpService.SelectSubtitleFile(Files, new[] { "en", "ru" }));
    }

    [Fact]
    public void SelectSubtitleFile_MatchesLanguageFamily()
    {
        Assert.Equal(
            "/tmp/abc.en-US.vtt",
            YtDlpService.SelectSubtitleFile(new[] { "/tmp/abc.en-US.vtt" }, new[] { "en.*" }));
    }

    [Fact]
    public void SelectSubtitleFile_FallsBackToFirstFile()
    {
        Assert.Equal("/tmp/abc.en.vtt", YtDlpService.SelectSubtitleFile(Files, new[] { "de" }));
    }

    [Fact]
    public void SelectSubtitleFile_EmptyList_ReturnsNull()
    {
        Assert.Null(YtDlpService.SelectSubtitleFile(Array.Empty<string>(), new[] { "en" }));
    }
}
