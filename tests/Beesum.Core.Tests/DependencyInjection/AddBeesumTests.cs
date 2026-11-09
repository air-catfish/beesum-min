using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Beesum.Core.Abstractions;
using Beesum.Core.DependencyInjection;
using Beesum.Core.Models;
using Beesum.Testing;
using Xunit;

namespace Beesum.Core.Tests.DependencyInjection;

public class AddBeesumTests
{
    [Fact]
    public void AddBeesum_RegistersStorage()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "beesum-di-" + Guid.NewGuid().ToString("N"));
        try
        {
            var services = new ServiceCollection();
            services.AddBeesum(options =>
            {
                options.YtDlpPath = "yt-dlp";
                options.OllamaBaseUrl = null;
                options.StorageRoot = tempDir;
            });
            using var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<ISettingsStore>());
            Assert.NotNull(provider.GetService<ISecretStore>());
            Assert.NotNull(provider.GetService<ITranscriptCache>());
            Assert.NotNull(provider.GetService<ISummaryHistory>());
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AddBeesum_ExposesConfiguredOptions()
    {
        var services = new ServiceCollection();
        services.AddBeesum(options =>
        {
            options.OllamaBaseUrl = null;
            options.OllamaModel = "test-model";
            options.LlmProviderId = "transcript-only";
            options.YtDlpExtraArgs.Add("--no-check-certificates");
        });
        using var provider = services.BuildServiceProvider();

        var resolved = provider.GetRequiredService<IOptions<BeesumOptions>>().Value;

        Assert.Equal("test-model", resolved.OllamaModel);
        Assert.Equal("transcript-only", resolved.LlmProviderId);
        Assert.Equal(new[] { "--no-check-certificates" }, resolved.YtDlpExtraArgs);
    }

    [Fact]
    public void AddBeesum_BlankProviderId_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(
            () => services.AddBeesum(options => options.LlmProviderId = " "));
    }

    [Fact]
    public async Task AddBeesum_ResolvesPipelineAndRunsOnLocalVtt()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "beesum-di-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var vttPath = Path.Combine(tempDir, "abc.en.vtt");
        await File.WriteAllTextAsync(vttPath, """
            WEBVTT

            00:00:00.000 --> 00:00:02.000
            Hello from DI.
            """);

        try
        {
            var services = new ServiceCollection();
            services.AddBeesum(options =>
            {
                options.YtDlpPath = "yt-dlp";
                options.OllamaBaseUrl = null; // skip Ollama; transcript-only is registered always
                options.LlmProviderId = "transcript-only";
            });

            await using var provider = services.BuildServiceProvider();
            var pipeline = provider.GetRequiredService<ISummarizerPipeline>();

            var result = await pipeline.RunAsync(
                new SummarizeCommand(string.Empty, PromptStyle.Neutral, OutputFormat.PlainText, vttPath),
                cancellationToken: CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Contains("Hello from DI.", result.Content, StringComparison.Ordinal);
            Assert.Equal("transcript-only", result.ProviderId);
            Assert.NotNull(result.Duration);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task AddBeesum_ForwardsYtDlpExtraArgsToService()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "beesum-di-" + Guid.NewGuid().ToString("N"));
        try
        {
            const string url = "https://example.com/watch?v=abc";
            const string json = """{"id":"abc","title":"t"}""";
            var runner = new InMemoryProcessRunner().EnqueueResult(0, json);

            var services = new ServiceCollection();
            services.AddBeesum(options =>
            {
                options.YtDlpPath = "yt-dlp";
                options.OllamaBaseUrl = null;
                options.StorageRoot = tempDir;
                options.YtDlpExtraArgs.Add("--proxy");
                options.YtDlpExtraArgs.Add("socks5://127.0.0.1:1080");
            });
            // Last registration wins: keep the real ProcessRunner out of the test.
            services.AddSingleton<IProcessRunner>(runner);

            await using var provider = services.BuildServiceProvider();
            var service = provider.GetRequiredService<IYtDlpService>();

            await service.GetInfoAsync(url, CancellationToken.None);

            var inv = Assert.Single(runner.Invocations);
            Assert.Equal(
                new[]
                {
                    "--dump-json", "--no-playlist", "--skip-download",
                    "--proxy", "socks5://127.0.0.1:1080",
                    "--", url,
                },
                inv.Arguments);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task AddBeesum_PipelineFactory_UnknownProviderReturnsFailure()
    {
        var services = new ServiceCollection();
        services.AddBeesum(options =>
        {
            options.YtDlpPath = "yt-dlp";
            options.OllamaBaseUrl = null;
        });
        await using var provider = services.BuildServiceProvider();

        var pipeline = provider
            .GetRequiredService<ISummarizerPipelineFactory>()
            .Create("missing");

        var result = await pipeline.RunAsync(
            new SummarizeCommand("https://example.com/watch?v=abc", PromptStyle.Neutral, OutputFormat.PlainText),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("missing", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void AddBeesumCore_LegacyOverload_StillRegisters()
    {
        var services = new ServiceCollection();
#pragma warning disable CS0618
        services.AddBeesumCore(ytDlpPath: "yt-dlp", ollamaBaseUrl: null);
#pragma warning restore CS0618
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<ISummarizerPipeline>());
        Assert.NotNull(provider.GetService<ISummarizerPipelineFactory>());
    }
}
