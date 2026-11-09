using Beesum.Testing;
using Beesum.Core.YtDlp;
using Xunit;

namespace Beesum.Core.Tests.YtDlp;

public class YtDlpServiceTests
{
    private const string YtDlpPath = "yt-dlp";
    private const string Url = "https://www.youtube.com/watch?v=abc123";

    /// <summary>Default service: static resolver with Explicit source (like CLI --yt-dlp).</summary>
    private static YtDlpService CreateService(InMemoryProcessRunner runner) =>
        new(runner, new StaticYtDlpPathResolver(YtDlpPath));

    /// <summary>Service where resolver reports a managed binary (needed for UpdateAsync).</summary>
    private static YtDlpService CreateManagedService(InMemoryProcessRunner runner) =>
        new(runner, new InMemoryYtDlpPathResolver
        {
            Result = new YtDlpResolution(YtDlpPath, YtDlpSource.Managed),
        });

    [Fact]
    public async Task GetInfoAsync_ValidJson_ReturnsVideoInfo()
    {
        const string json = """{"id":"abc123","title":"My Video","duration":123.5,"uploader":"Channel","thumbnail":"https://i.ytimg.com/x.jpg"}""";
        var runner = new InMemoryProcessRunner().EnqueueResult(0, json);

        var info = await CreateService(runner).GetInfoAsync(Url, CancellationToken.None);

        Assert.NotNull(info);
        Assert.Equal("abc123", info.Id);
        Assert.Equal("My Video", info.Title);
        Assert.Equal(TimeSpan.FromSeconds(123.5), info.Duration);
        Assert.Equal("Channel", info.Author);
        Assert.Equal("https://i.ytimg.com/x.jpg", info.ThumbnailUrl);
    }

    [Fact]
    public async Task GetInfoAsync_NonZeroExit_ReturnsNull()
    {
        var runner = new InMemoryProcessRunner().EnqueueResult(1, "", "error");

        Assert.Null(await CreateService(runner).GetInfoAsync(Url, CancellationToken.None));
    }

    [Fact]
    public async Task GetInfoAsync_InvalidJson_ReturnsNull()
    {
        var runner = new InMemoryProcessRunner().EnqueueResult(0, "not json at all");

        Assert.Null(await CreateService(runner).GetInfoAsync(Url, CancellationToken.None));
    }

    [Fact]
    public async Task GetInfoAsync_EmptyUrl_ReturnsNullWithoutInvokingProcess()
    {
        var runner = new InMemoryProcessRunner();

        Assert.Null(await CreateService(runner).GetInfoAsync("", CancellationToken.None));
        Assert.Empty(runner.Invocations);
    }

    [Fact]
    public async Task GetInfoAsync_PassesExpectedArguments()
    {
        const string json = """{"id":"abc","title":"t","duration":1}""";
        var runner = new InMemoryProcessRunner().EnqueueResult(0, json);

        await CreateService(runner).GetInfoAsync(Url, CancellationToken.None);

        var inv = Assert.Single(runner.Invocations);
        Assert.Equal(YtDlpPath, inv.FileName);
        Assert.Equal(new[] { "--dump-json", "--no-playlist", "--skip-download", Url }, inv.Arguments);
    }

    [Fact]
    public async Task UpdateAsync_Success_ReturnsTrueAndPassesUpdateFlag()
    {
        var runner = new InMemoryProcessRunner().EnqueueResult(0, "Updated");

        Assert.True(await CreateManagedService(runner).UpdateAsync(CancellationToken.None));
        var inv = Assert.Single(runner.Invocations);
        Assert.Equal(new[] { "-U" }, inv.Arguments);
    }

    [Fact]
    public async Task UpdateAsync_NonZero_ReturnsFalse()
    {
        var runner = new InMemoryProcessRunner().EnqueueResult(1, "", "fail");

        Assert.False(await CreateManagedService(runner).UpdateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_PathSource_ReturnsFalseWithoutRunningProcess()
    {
        var runner = new InMemoryProcessRunner();
        var resolver = new InMemoryYtDlpPathResolver
        {
            Result = new YtDlpResolution("yt-dlp", YtDlpSource.Path),
        };
        var service = new YtDlpService(runner, resolver);

        Assert.False(await service.UpdateAsync(CancellationToken.None));
        Assert.Empty(runner.Invocations);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_NoHttpClient_ReturnsFalseWithoutInvokingProcess()
    {
        var runner = new InMemoryProcessRunner();

        Assert.False(await CreateService(runner).CheckForUpdatesAsync(CancellationToken.None));
        Assert.Empty(runner.Invocations);
    }

    [Fact]
    public async Task DownloadSubtitlesAsync_Success_ReturnsFilePathAndLanguage()
    {
        var tempDir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "abc123.en.vtt"), "WEBVTT\n\n");
            var runner = new InMemoryProcessRunner().EnqueueResult(0);

            var result = await CreateService(runner).DownloadSubtitlesAsync(Url, tempDir, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.FilePath);
            Assert.EndsWith("abc123.en.vtt", result.FilePath);
            Assert.Equal("en", result.Language);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public async Task DownloadSubtitlesAsync_NonZeroExit_ReturnsFailureWithStderr()
    {
        var tempDir = CreateTempDir();
        try
        {
            var runner = new InMemoryProcessRunner().EnqueueResult(1, "", "video unavailable");

            var result = await CreateService(runner).DownloadSubtitlesAsync(Url, tempDir, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Contains("video unavailable", result.ErrorMessage, StringComparison.Ordinal);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public async Task DownloadSubtitlesAsync_ExitZeroButNoFile_ReturnsFailure()
    {
        var tempDir = CreateTempDir();
        try
        {
            var runner = new InMemoryProcessRunner().EnqueueResult(0);

            var result = await CreateService(runner).DownloadSubtitlesAsync(Url, tempDir, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public async Task DownloadSubtitlesAsync_PassesExpectedArguments()
    {
        var tempDir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "abc.en.vtt"), "WEBVTT");
            var runner = new InMemoryProcessRunner().EnqueueResult(0);

            await CreateService(runner).DownloadSubtitlesAsync(Url, tempDir, CancellationToken.None);

            var inv = Assert.Single(runner.Invocations);
            Assert.Contains("--write-subs", inv.Arguments);
            Assert.Contains("--sub-format", inv.Arguments);
            Assert.Contains("vtt", inv.Arguments);
            Assert.Contains("--skip-download", inv.Arguments);
            Assert.Contains("--no-playlist", inv.Arguments);
            Assert.Contains(Url, inv.Arguments);
            Assert.Equal(tempDir, inv.WorkingDirectory);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public async Task DownloadSubtitlesAsync_ResolverReturnsNull_ReturnsFailureWithHelpText()
    {
        var tempDir = CreateTempDir();
        try
        {
            var runner = new InMemoryProcessRunner();
            var service = new YtDlpService(runner, new InMemoryYtDlpPathResolver { Result = null });

            var result = await service.DownloadSubtitlesAsync(Url, tempDir, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Contains("yt-dlp not found", result.ErrorMessage, StringComparison.Ordinal);
            Assert.Empty(runner.Invocations);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "beesum-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
