using Beesum.Testing;
using Beesum.Core.YtDlp;
using Xunit;

namespace Beesum.Core.Tests.YtDlp;

/// <summary>
/// Per-call subtitle languages: they override the configured defaults, are validated
/// before reaching yt-dlp, and a missing language produces an actionable message.
/// </summary>
public class YtDlpServiceLanguageTests
{
    private const string YtDlpPath = "yt-dlp";
    private const string Url = "https://www.youtube.com/watch?v=abc123";

    private static YtDlpService CreateService(InMemoryProcessRunner runner) =>
        new(runner, new StaticYtDlpPathResolver(YtDlpPath));

    [Fact]
    public async Task DownloadSubtitlesAsync_PerCallLanguages_OverrideConfiguredDefaults()
    {
        var tempDir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "abc123.ru.vtt"), "WEBVTT\n\n");
            var runner = new InMemoryProcessRunner().EnqueueResult(0);

            var result = await CreateService(runner)
                .DownloadSubtitlesAsync(Url, tempDir, new[] { "ru", "en" }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("ru", result.Language);
            var inv = Assert.Single(runner.Invocations);
            Assert.Equal(
                new[]
                {
                    "--write-subs", "--write-auto-subs",
                    "--sub-format", "vtt",
                    "--sub-langs", "ru,en",
                    "--skip-download", "--no-playlist",
                    "-o", "%(id)s.%(ext)s",
                    "--", Url,
                },
                inv.Arguments);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public async Task DownloadSubtitlesAsync_EmptyLanguageList_UsesConfiguredDefaults()
    {
        var tempDir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "abc123.en.vtt"), "WEBVTT\n\n");
            var runner = new InMemoryProcessRunner().EnqueueResult(0);

            await CreateService(runner)
                .DownloadSubtitlesAsync(Url, tempDir, Array.Empty<string>(), CancellationToken.None);

            var inv = Assert.Single(runner.Invocations);
            var args = inv.Arguments.ToArray();
            var index = Array.IndexOf(args, "--sub-langs");
            Assert.True(index >= 0);
            Assert.Equal("en", args[index + 1]);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Theory]
    [InlineData("--evil")]
    [InlineData("-all")]
    [InlineData("en,ru")]
    [InlineData("")]
    [InlineData("this-language-code-is-far-too-long")]
    public async Task DownloadSubtitlesAsync_InvalidLanguage_FailsWithoutRunningProcess(string language)
    {
        var tempDir = CreateTempDir();
        try
        {
            var runner = new InMemoryProcessRunner();

            var result = await CreateService(runner)
                .DownloadSubtitlesAsync(Url, tempDir, new[] { language }, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Contains("Invalid subtitle language", result.ErrorMessage, StringComparison.Ordinal);
            Assert.Empty(runner.Invocations);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("en-US")]
    [InlineData("ja-orig")]
    [InlineData("en.*")]
    public async Task DownloadSubtitlesAsync_ValidLanguageSelectors_AreAccepted(string language)
    {
        var tempDir = CreateTempDir();
        try
        {
            var runner = new InMemoryProcessRunner().EnqueueResult(0);

            await CreateService(runner)
                .DownloadSubtitlesAsync(Url, tempDir, new[] { language }, CancellationToken.None);

            Assert.NotEmpty(runner.Invocations);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public async Task DownloadSubtitlesAsync_NoSubtitlesForLanguage_ListsAvailableOnes()
    {
        const string info =
            """{"id":"abc123","subtitles":{"en":[],"de":[],"live_chat":[]},"automatic_captions":{"ja-orig":[],"en":[],"ru":[]}}""";
        var tempDir = CreateTempDir();
        try
        {
            var runner = new InMemoryProcessRunner()
                .EnqueueResult(0)
                .EnqueueResult(0, info);

            var result = await CreateService(runner)
                .DownloadSubtitlesAsync(Url, tempDir, new[] { "ru" }, CancellationToken.None);

            Assert.False(result.IsSuccess);
            var message = result.ErrorMessage;
            Assert.NotNull(message);
            Assert.StartsWith("No .vtt file was produced.", message, StringComparison.Ordinal);
            Assert.Contains("No subtitles for 'ru'", message, StringComparison.Ordinal);
            Assert.Contains("manual subtitles: de, en", message, StringComparison.Ordinal);
            Assert.Contains("original language): ja-orig", message, StringComparison.Ordinal);
            Assert.DoesNotContain("live_chat", message, StringComparison.Ordinal);

            Assert.Equal(2, runner.Invocations.Count);
            Assert.Contains("--dump-json", runner.Invocations[1].Arguments);
            Assert.Contains("--skip-download", runner.Invocations[1].Arguments);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public async Task DownloadSubtitlesAsync_VideoWithoutAnySubtitles_SaysSo()
    {
        const string info = """{"id":"abc123","subtitles":{},"automatic_captions":{}}""";
        var tempDir = CreateTempDir();
        try
        {
            var runner = new InMemoryProcessRunner()
                .EnqueueResult(0)
                .EnqueueResult(0, info);

            var result = await CreateService(runner)
                .DownloadSubtitlesAsync(Url, tempDir, new[] { "en" }, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Contains("no subtitles or automatic captions", result.ErrorMessage, StringComparison.Ordinal);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public async Task DownloadSubtitlesAsync_LanguageListingFails_KeepsPlainMessage()
    {
        var tempDir = CreateTempDir();
        try
        {
            var runner = new InMemoryProcessRunner()
                .EnqueueResult(0)
                .EnqueueResult(1, "", "boom");

            var result = await CreateService(runner)
                .DownloadSubtitlesAsync(Url, tempDir, new[] { "ru" }, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal("No .vtt file was produced.", result.ErrorMessage);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "beesum-lang-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
