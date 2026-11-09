using Beesum.Core.Abstractions;
using Beesum.Core.DependencyInjection;
using Beesum.Core.Models;
using Beesum.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Beesum.Testing.Tests;

public class AddBeesumTestingTests
{
    [Fact]
    public void AddBeesumTesting_ReplacesCoreServices()
    {
        var services = new ServiceCollection();
        services.AddBeesum(options =>
        {
            options.YtDlpPath = "yt-dlp";
            options.OllamaBaseUrl = null;
        });
        services.AddBeesumTesting();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<InMemorySettingsStore>(provider.GetRequiredService<ISettingsStore>());
        Assert.IsType<InMemorySecretStore>(provider.GetRequiredService<ISecretStore>());
        Assert.IsType<InMemoryTranscriptCache>(provider.GetRequiredService<ITranscriptCache>());
        Assert.IsType<InMemorySummaryHistory>(provider.GetRequiredService<ISummaryHistory>());
        Assert.IsType<InMemorySubtitleParser>(provider.GetRequiredService<ISubtitleParser>());
        Assert.IsType<InMemoryProcessRunner>(provider.GetRequiredService<IProcessRunner>());
        Assert.IsType<InMemoryYtDlpPathResolver>(provider.GetRequiredService<IYtDlpPathResolver>());
        Assert.IsType<InMemoryYtDlpService>(provider.GetRequiredService<IYtDlpService>());
        Assert.IsType<InMemoryLlmProvider>(Assert.Single(provider.GetServices<ILlmProvider>()));
    }

    [Fact]
    public async Task AddBeesumTesting_PipelineRunsWithoutDiskOrNetwork()
    {
        var vttPath = Path.Combine(
            Path.GetTempPath(), "beesum-testing-" + Guid.NewGuid().ToString("N") + ".vtt");
        await File.WriteAllTextAsync(vttPath, "WEBVTT\n\n00:00:00.000 --> 00:00:01.000\nHello\n");

        try
        {
            var services = new ServiceCollection();
            services.AddBeesum(options =>
            {
                options.YtDlpPath = "yt-dlp";
                options.OllamaBaseUrl = null;
            });
            services.AddBeesumTesting();

            await using var provider = services.BuildServiceProvider();

            var parser = (InMemorySubtitleParser)provider.GetRequiredService<ISubtitleParser>();
            parser.ParseResult = new Transcript("abc", "en", new[]
            {
                new SubtitleSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "Hello"),
            });

            var pipeline = provider.GetRequiredService<ISummarizerPipeline>();
            var result = await pipeline.RunAsync(
                new SummarizeCommand(string.Empty, PromptStyle.Neutral, OutputFormat.PlainText, vttPath),
                cancellationToken: CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("summary", result.Content);
            Assert.NotNull(result.Duration);

            var providerDouble = (InMemoryLlmProvider)Assert.Single(provider.GetServices<ILlmProvider>());
            Assert.Equal(1, providerDouble.SummarizeCallCount);
        }
        finally
        {
            File.Delete(vttPath);
        }
    }

    [Fact]
    public async Task InMemorySummaryHistory_ReturnsNewestFirst()
    {
        var history = new InMemorySummaryHistory();
        var older = Entry("a", DateTimeOffset.UnixEpoch);
        var newer = Entry("b", DateTimeOffset.UnixEpoch.AddMinutes(1));

        await history.AddAsync(older);
        await history.AddAsync(newer);

        var entries = await history.GetAllAsync();

        Assert.Equal(new[] { "b", "a" }, entries.Select(e => e.Id));

        await history.RemoveAsync("b");
        Assert.Single(await history.GetAllAsync());
    }

    private static SummaryHistoryEntry Entry(string id, DateTimeOffset createdAt) =>
        new(id, "src", "title", createdAt, new SummaryResult(true, "content", "in-memory"));
}
