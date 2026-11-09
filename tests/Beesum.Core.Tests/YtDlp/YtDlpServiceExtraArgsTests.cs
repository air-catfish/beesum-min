using Beesum.Testing;
using Beesum.Core.YtDlp;
using Xunit;

namespace Beesum.Core.Tests.YtDlp;

/// <summary>
/// Phase 9c-3a: extra arguments (--proxy, --cookies-from-browser, ...) must reach
/// every yt-dlp invocation, after the built-in flags and before the URL.
/// </summary>
public class YtDlpServiceExtraArgsTests
{
    private const string YtDlpPath = "yt-dlp";
    private const string Url = "https://www.youtube.com/watch?v=abc123";

    private static readonly string[] ExtraArgs =
    {
        "--proxy", "socks5://127.0.0.1:1080",
        "--sleep-interval", "2",
    };

    private static YtDlpService CreateService(InMemoryProcessRunner runner) =>
        new(runner, new StaticYtDlpPathResolver(YtDlpPath),
            httpClient: null,
            extraArgs: ExtraArgs);

    private static YtDlpService CreateManagedService(
        InMemoryProcessRunner runner,
        HttpClient? httpClient = null) =>
        new(runner,
            new InMemoryYtDlpPathResolver { Result = new YtDlpResolution(YtDlpPath, YtDlpSource.Managed) },
            httpClient,
            ExtraArgs);

    [Fact]
    public async Task GetInfoAsync_InsertsExtraArgsBeforeUrl()
    {
        const string json = """{"id":"abc123","title":"My Video","duration":10}""";
        var runner = new InMemoryProcessRunner().EnqueueResult(0, json);

        await CreateService(runner).GetInfoAsync(Url, CancellationToken.None);

        var inv = Assert.Single(runner.Invocations);
        Assert.Equal(
            new[]
            {
                "--dump-json", "--no-playlist", "--skip-download",
                "--proxy", "socks5://127.0.0.1:1080",
                "--sleep-interval", "2",
                Url,
            },
            inv.Arguments);
    }

    [Fact]
    public async Task DownloadSubtitlesAsync_InsertsExtraArgsAfterBaseFlagsAndBeforeUrl()
    {
        var tempDir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "abc123.en.vtt"), "WEBVTT\n\n");
            var runner = new InMemoryProcessRunner().EnqueueResult(0);

            await CreateService(runner).DownloadSubtitlesAsync(Url, tempDir, CancellationToken.None);

            var inv = Assert.Single(runner.Invocations);
            Assert.Equal(
                new[]
                {
                    "--write-subs", "--write-auto-subs",
                    "--sub-format", "vtt",
                    "--sub-langs", "en",
                    "--skip-download", "--no-playlist",
                    "-o", "%(id)s.%(ext)s",
                    "--proxy", "socks5://127.0.0.1:1080",
                    "--sleep-interval", "2",
                    Url,
                },
                inv.Arguments);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public async Task UpdateAsync_InsertsExtraArgsAfterUpdateFlag()
    {
        var runner = new InMemoryProcessRunner().EnqueueResult(0, "Updated");

        Assert.True(await CreateManagedService(runner).UpdateAsync(CancellationToken.None));

        var inv = Assert.Single(runner.Invocations);
        Assert.Equal(
            new[]
            {
                "-U",
                "--proxy", "socks5://127.0.0.1:1080",
                "--sleep-interval", "2",
            },
            inv.Arguments);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_InsertsExtraArgsAfterVersionFlag()
    {
        var handler = new InMemoryHttpMessageHandler()
            .EnqueueJson("""{"tag_name":"2025.02.02"}""");
        var runner = new InMemoryProcessRunner().EnqueueResult(0, "2025.01.01\n");
        var service = CreateManagedService(runner, new HttpClient(handler));

        Assert.True(await service.CheckForUpdatesAsync(CancellationToken.None));

        var inv = Assert.Single(runner.Invocations);
        Assert.Equal(
            new[]
            {
                "--version",
                "--proxy", "socks5://127.0.0.1:1080",
                "--sleep-interval", "2",
            },
            inv.Arguments);
    }

    [Fact]
    public async Task Constructor_NullExtraArgs_KeepsLegacyArguments()
    {
        const string json = """{"id":"abc","title":"t","duration":1}""";
        var runner = new InMemoryProcessRunner()
            .EnqueueResult(0, json)
            .EnqueueResult(0);
        var service = new YtDlpService(
            runner,
            new StaticYtDlpPathResolver(YtDlpPath),
            httpClient: null,
            extraArgs: null);
        var updateRunner = new InMemoryProcessRunner().EnqueueResult(0, "ok");
        var managed = new YtDlpService(
            updateRunner,
            new InMemoryYtDlpPathResolver { Result = new YtDlpResolution(YtDlpPath, YtDlpSource.Managed) },
            httpClient: null,
            extraArgs: null);
        var tempDir = CreateTempDir();
        try
        {
            await service.GetInfoAsync(Url, CancellationToken.None);
            await service.DownloadSubtitlesAsync(Url, tempDir, CancellationToken.None);
            Assert.True(await managed.UpdateAsync(CancellationToken.None));

            Assert.Equal(new[] { "-U" }, updateRunner.Invocations[0].Arguments);
            Assert.Equal(
                new[] { "--dump-json", "--no-playlist", "--skip-download", Url },
                runner.Invocations[0].Arguments);
            Assert.Equal(
                new[]
                {
                    "--write-subs", "--write-auto-subs",
                    "--sub-format", "vtt",
                    "--sub-langs", "en",
                    "--skip-download", "--no-playlist",
                    "-o", "%(id)s.%(ext)s",
                    Url,
                },
                runner.Invocations[1].Arguments);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "beesum-extra-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
