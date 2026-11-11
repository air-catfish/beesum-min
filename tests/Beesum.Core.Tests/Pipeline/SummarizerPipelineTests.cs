using Beesum.Core.Abstractions;
using Beesum.Core.Llm;
using Beesum.Core.Models;
using Beesum.Core.Pipeline;
using Beesum.Testing;
using Xunit;

namespace Beesum.Core.Tests.Pipeline;

public class SummarizerPipelineTests
{
    private const string Url = "https://youtube.com/watch?v=abc";

    private static SummarizeCommand Command(string? localVtt = null) =>
        new(Url, PromptStyle.Neutral, OutputFormat.PlainText, localVtt);

    private static InMemorySubtitleParser Parser() => new()
    {
        ParseResult = new Transcript("abc", "en", new[]
        {
            new SubtitleSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "Hi"),
        }),
    };

    private static string WriteTempVtt(string name = "abc.en.vtt")
    {
        var dir = Path.Combine(Path.GetTempPath(), "beesum-pipe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, "WEBVTT\n\n00:00:00.000 --> 00:00:01.000\nHi\n");
        return path;
    }

    [Fact]
    public async Task RunAsync_NoLocalVtt_DownloadsAndParsesAndSummarizes()
    {
        var tempVtt = WriteTempVtt();
        try
        {
            var ytDlp = new InMemoryYtDlpService
            {
                DownloadResult = new(true, tempVtt, "en"),
            };
            var parser = new InMemorySubtitleParser
            {
                ParseResult = new Transcript("abc", "en", new[]
                {
                    new SubtitleSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "Hi"),
                }),
            };
            var provider = new InMemoryLlmProvider();
            var pipeline = new SummarizerPipeline(ytDlp, parser, new[] { provider });

            var result = await pipeline.RunAsync(Command(), cancellationToken: CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Single(ytDlp.DownloadCalls);
            Assert.Single(parser.ParseFileCalls);
            Assert.Equal("abc", parser.ParseFileCalls[0].SourceId);
            Assert.Equal(1, provider.SummarizeCallCount);
            Assert.NotNull(result.Duration);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(tempVtt)!, true);
        }
    }

    [Fact]
    public async Task RunAsync_LocalVttProvided_SkipsDownload()
    {
        var tempVtt = WriteTempVtt();
        try
        {
            var ytDlp = new InMemoryYtDlpService();
            var parser = Parser();
            var provider = new InMemoryLlmProvider();
            var pipeline = new SummarizerPipeline(ytDlp, parser, new[] { provider });

            var result = await pipeline.RunAsync(Command(localVtt: tempVtt), cancellationToken: CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(ytDlp.DownloadCalls);
            Assert.Single(parser.ParseFileCalls);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(tempVtt)!, true);
        }
    }

    [Fact]
    public async Task RunAsync_LocalVttDoesNotExist_ReturnsFailure()
    {
        var ytDlp = new InMemoryYtDlpService();
        var pipeline = new SummarizerPipeline(
            ytDlp,
            Parser(),
            new[] { new InMemoryLlmProvider() });

        var result = await pipeline.RunAsync(
            Command(localVtt: "/nonexistent/file.vtt"),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(ytDlp.DownloadCalls);
    }

    [Fact]
    public async Task RunAsync_DownloadFailure_ReturnsFailureWithoutParsing()
    {
        var ytDlp = new InMemoryYtDlpService
        {
            DownloadResult = new(false, ErrorMessage: "video unavailable"),
        };
        var parser = Parser();
        var pipeline = new SummarizerPipeline(ytDlp, parser, new[] { new InMemoryLlmProvider() });

        var result = await pipeline.RunAsync(Command(), cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("video unavailable", result.ErrorMessage);
        Assert.Empty(parser.ParseFileCalls);
    }

    [Fact]
    public async Task RunAsync_UnknownProvider_ReturnsFailureWithoutDownloading()
    {
        var ytDlp = new InMemoryYtDlpService();
        var pipeline = new SummarizerPipeline(
            ytDlp,
            Parser(),
            new[] { new InMemoryLlmProvider("other") },
            providerId: "missing");

        var result = await pipeline.RunAsync(Command(), cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("missing", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(ytDlp.DownloadCalls);
    }

    [Fact]
    public async Task RunAsync_ReportsStagesInOrder()
    {
        var tempVtt = WriteTempVtt();
        try
        {
            var stages = new List<PipelineStage>();
            var progress = new InlineProgress(stages);
            var ytDlp = new InMemoryYtDlpService { DownloadResult = new(true, tempVtt, "en") };
            var pipeline = new SummarizerPipeline(
                ytDlp,
                Parser(),
                new[] { new InMemoryLlmProvider() });

            await pipeline.RunAsync(Command(), progress, CancellationToken.None);

            Assert.Equal(
                new[]
                {
                    PipelineStage.Starting,
                    PipelineStage.Downloading,
                    PipelineStage.Parsing,
                    PipelineStage.Summarizing,
                    PipelineStage.Completed,
                },
                stages);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(tempVtt)!, true);
        }
    }

    [Fact]
    public async Task RunAsync_SummarizeFailure_DoesNotReportCompleted()
    {
        var tempVtt = WriteTempVtt();
        try
        {
            var stages = new List<PipelineStage>();
            var progress = new InlineProgress(stages);
            var ytDlp = new InMemoryYtDlpService { DownloadResult = new(true, tempVtt, "en") };
            var provider = new InMemoryLlmProvider
            {
                Result = new SummaryResult(false, string.Empty, "fake", "llm down"),
            };
            var pipeline = new SummarizerPipeline(ytDlp, Parser(), new[] { provider });

            var result = await pipeline.RunAsync(Command(), progress, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.DoesNotContain(PipelineStage.Completed, stages);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(tempVtt)!, true);
        }
    }

    [Fact]
    public async Task RunAsync_CallerCancellation_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var pipeline = new SummarizerPipeline(
            new InMemoryYtDlpService(),
            Parser(),
            new[] { new InMemoryLlmProvider() });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pipeline.RunAsync(Command(), cancellationToken: cts.Token));
    }

    [Fact]
    public async Task RunAsync_ConfiguredSubtitlesDirectoryNotWritable_FallsBackToWritableDirectory()
    {
        var tempVtt = WriteTempVtt();
        var blocked = Path.Combine(
            Path.GetTempPath(), "beesum-blocked-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(blocked, "a file, not a directory");
        try
        {
            var ytDlp = new InMemoryYtDlpService { DownloadResult = new(true, tempVtt, "en") };
            var pipeline = new SummarizerPipeline(
                ytDlp,
                Parser(),
                new[] { new InMemoryLlmProvider() },
                subtitlesDirectory: blocked);

            var result = await pipeline.RunAsync(Command(), cancellationToken: CancellationToken.None);

            Assert.True(result.IsSuccess);
            var workDir = Assert.Single(ytDlp.DownloadCalls).OutputDirectory;
            Assert.NotEqual(blocked, workDir);
            Assert.False(Directory.Exists(workDir)); // fallback directory is temporary
        }
        finally
        {
            File.Delete(blocked);
            Directory.Delete(Path.GetDirectoryName(tempVtt)!, true);
        }
    }

    [Fact]
    public async Task RunAsync_ConfiguredSubtitlesDirectoryWritable_IsUsedAndKept()
    {
        var tempVtt = WriteTempVtt();
        var cacheDir = Path.Combine(
            Path.GetTempPath(), "beesum-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDir);
        try
        {
            var ytDlp = new InMemoryYtDlpService { DownloadResult = new(true, tempVtt, "en") };
            var pipeline = new SummarizerPipeline(
                ytDlp,
                Parser(),
                new[] { new InMemoryLlmProvider() },
                subtitlesDirectory: cacheDir);

            var result = await pipeline.RunAsync(Command(), cancellationToken: CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(cacheDir, Assert.Single(ytDlp.DownloadCalls).OutputDirectory);
            Assert.True(Directory.Exists(cacheDir));
        }
        finally
        {
            Directory.Delete(cacheDir, recursive: true);
            Directory.Delete(Path.GetDirectoryName(tempVtt)!, true);
        }
    }

    [Fact]
    public async Task RunAsync_ForwardsPromptOverrides()
    {
        var tempVtt = WriteTempVtt();
        try
        {
            var provider = new InMemoryLlmProvider();
            var pipeline = new SummarizerPipeline(
                new InMemoryYtDlpService { DownloadResult = new(true, tempVtt, "en") },
                Parser(),
                new[] { provider });

            var command = new SummarizeCommand(
                Url,
                PromptStyle.Neutral,
                OutputFormat.PlainText,
                Instructions: "five bullets",
                SystemPrompt: "You are terse.");

            await pipeline.RunAsync(command, cancellationToken: CancellationToken.None);

            var request = Assert.Single(provider.Requests);
            Assert.Equal("five bullets", request.Instructions);
            Assert.Equal("You are terse.", request.SystemPrompt);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(tempVtt)!, true);
        }
    }    [Fact]
    public async Task RunAsync_PreParsedTranscript_SkipsDownloadAndParsing()
    {
        var ytDlp = new InMemoryYtDlpService();
        var parser = Parser();
        var provider = new InMemoryLlmProvider();
        var pipeline = new SummarizerPipeline(ytDlp, parser, new[] { provider });

        var transcript = new Transcript("abc", "en", new[]
        {
            new SubtitleSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "Hi"),
        });

        var result = await pipeline.RunAsync(
            new SummarizeCommand(
                Url: string.Empty,
                PromptStyle.Neutral,
                OutputFormat.PlainText,
                Transcript: transcript),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(ytDlp.DownloadCalls);
        Assert.Empty(parser.ParseFileCalls);
        Assert.Single(provider.Requests);
    }    [Fact]
    public async Task RunAsync_LongTranscript_SplitsAndCombines()
    {
        var tempVtt = WriteTempVtt();
        try
        {
            var parser = new InMemorySubtitleParser { ParseResult = LongTranscript() };
            var provider = new InMemoryLlmProvider();
            var pipeline = new SummarizerPipeline(
                new InMemoryYtDlpService { DownloadResult = new(true, tempVtt, "en") },
                parser,
                new[] { provider },
                chunkTokens: 20);

            var result = await pipeline.RunAsync(Command(), cancellationToken: CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(provider.SummarizeCallCount > 2);
            Assert.Contains("part 1 of", provider.Requests[0].Instructions, StringComparison.Ordinal);
            Assert.Contains("partial summaries", provider.Requests[^1].Instructions, StringComparison.Ordinal);
            Assert.NotNull(result.Duration);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(tempVtt)!, true);
        }
    }

    [Fact]
    public async Task RunAsync_LongTranscript_SumsUsage()
    {
        var tempVtt = WriteTempVtt();
        try
        {
            var provider = new InMemoryLlmProvider();
            for (var i = 0; i < 20; i++)
                provider.Enqueue(new SummaryResult(true, $"partial {i}", "in-memory", Usage: new TokenUsage(1, 1, 2)));

            var pipeline = new SummarizerPipeline(
                new InMemoryYtDlpService { DownloadResult = new(true, tempVtt, "en") },
                new InMemorySubtitleParser { ParseResult = LongTranscript() },
                new[] { provider },
                chunkTokens: 20);

            var result = await pipeline.RunAsync(Command(), cancellationToken: CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Usage);
            Assert.Equal(provider.SummarizeCallCount * 2, result.Usage.TotalTokens);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(tempVtt)!, true);
        }
    }

    [Fact]
    public async Task RunAsync_ChunkFailure_ReturnsChunkError()
    {
        var tempVtt = WriteTempVtt();
        try
        {
            var provider = new InMemoryLlmProvider()
                .Enqueue(new SummaryResult(true, "ok", "in-memory"))
                .Enqueue(new SummaryResult(false, string.Empty, "in-memory", "boom"));

            var pipeline = new SummarizerPipeline(
                new InMemoryYtDlpService { DownloadResult = new(true, tempVtt, "en") },
                new InMemorySubtitleParser { ParseResult = LongTranscript() },
                new[] { provider },
                chunkTokens: 20);

            var result = await pipeline.RunAsync(Command(), cancellationToken: CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Contains("Chunk 2/", result.ErrorMessage, StringComparison.Ordinal);
            Assert.Contains("boom", result.ErrorMessage, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(tempVtt)!, true);
        }
    }

    [Fact]
    public async Task RunAsync_ChunkingDisabled_KeepsOneCall()
    {
        var tempVtt = WriteTempVtt();
        try
        {
            var provider = new InMemoryLlmProvider();
            var pipeline = new SummarizerPipeline(
                new InMemoryYtDlpService { DownloadResult = new(true, tempVtt, "en") },
                new InMemorySubtitleParser { ParseResult = LongTranscript() },
                new[] { provider },
                chunkingEnabled: false,
                chunkTokens: 20);

            var result = await pipeline.RunAsync(Command(), cancellationToken: CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, provider.SummarizeCallCount);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(tempVtt)!, true);
        }
    }
    [Fact]
    public async Task RunAsync_EmptyTranscript_ReturnsFailure()
    {
        var provider = new InMemoryLlmProvider();
        var pipeline = new SummarizerPipeline(
            new InMemoryYtDlpService(),
            Parser(),
            new[] { provider });

        var command = new SummarizeCommand(
            Url: string.Empty,
            PromptStyle.Neutral,
            OutputFormat.PlainText,
            Transcript: new Transcript("abc", "en", Array.Empty<SubtitleSegment>()));

        var result = await pipeline.RunAsync(command, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("No subtitle text", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, provider.SummarizeCallCount);
    }
    [Fact]
    public async Task RunAsync_Timestamps_ForwardsToProvider()
    {
        var provider = new InMemoryLlmProvider();
        var pipeline = new SummarizerPipeline(
            new InMemoryYtDlpService(),
            Parser(),
            new[] { provider });

        var command = new SummarizeCommand(
            Url: string.Empty,
            PromptStyle.Neutral,
            OutputFormat.PlainText,
            Transcript: new Transcript("abc", "en", new[]
            {
                new SubtitleSegment(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(7), "Hi"),
            }),
            Timestamps: true);

        var result = await pipeline.RunAsync(command, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var request = Assert.Single(provider.Requests);
        Assert.True(request.Timestamps);
        Assert.Equal("[00:00:05] Hi", request.Transcript.ToPlainText(timestamps: true));
    }
    [Fact]
    public async Task RunAsync_MindMap_NormalizesProviderOutline()
    {
        var provider = new InMemoryLlmProvider("in-memory")
        {
            Result = new SummaryResult(true, "- Caching\n  - Keys\n  - Invalidation", "in-memory"),
        };
        var pipeline = new SummarizerPipeline(
            new InMemoryYtDlpService(),
            Parser(),
            new[] { provider },
            providerId: "in-memory");

        var command = new SummarizeCommand(
            Url: string.Empty,
            PromptStyle.Neutral,
            OutputFormat.MindMap,
            Transcript: new Transcript("abc", "en", new[]
            {
                new SubtitleSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "Hi"),
            }));

        var result = await pipeline.RunAsync(command, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("```mermaid", result.Content, StringComparison.Ordinal);
        Assert.Contains("root((Caching))", result.Content, StringComparison.Ordinal);
    }
    [Fact]
    public async Task RunAsync_SaveVtt_CopiesTheDownloadedFile()
    {
        var tempVtt = WriteTempVtt();
        var target = Path.Combine(Path.GetTempPath(), "beesum-saved-" + Guid.NewGuid().ToString("N") + ".vtt");
        try
        {
            var pipeline = new SummarizerPipeline(
                new InMemoryYtDlpService(),
                Parser(),
                new[] { new InMemoryLlmProvider() });

            var command = new SummarizeCommand(
                Url,
                PromptStyle.Neutral,
                OutputFormat.PlainText,
                LocalVttPath: tempVtt,
                SaveVttPath: target);

            var result = await pipeline.RunAsync(command, cancellationToken: CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(File.Exists(target));
            Assert.Equal(await File.ReadAllTextAsync(tempVtt), await File.ReadAllTextAsync(target));
        }
        finally
        {
            File.Delete(target);
            Directory.Delete(Path.GetDirectoryName(tempVtt)!, true);
        }
    }

    [Fact]
    public async Task RunAsync_FromTo_FiltersCues()
    {
        var provider = new InMemoryLlmProvider("transcript-only");
        var pipeline = new SummarizerPipeline(
            new InMemoryYtDlpService(),
            Parser(),
            new[] { provider });

        var command = new SummarizeCommand(
            Url: string.Empty,
            PromptStyle.Neutral,
            OutputFormat.PlainText,
            Transcript: new Transcript("abc", "en", new[]
            {
                new SubtitleSegment(TimeSpan.Zero, TimeSpan.FromSeconds(10), "first"),
                new SubtitleSegment(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30), "second"),
            }),
            From: TimeSpan.FromSeconds(15),
            To: TimeSpan.FromSeconds(25));

        var result = await pipeline.RunAsync(command, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var request = Assert.Single(provider.Requests);
        Assert.Equal("second", request.Transcript.ToPlainText());
    }
    [Fact]
    public async Task RunAsync_SaveTranscript_WritesTheRawClippedText()
    {
        var target = Path.Combine(Path.GetTempPath(), "beesum-transcript-" + Guid.NewGuid().ToString("N") + ".md");
        try
        {
            var provider = new InMemoryLlmProvider
            {
                Result = new SummaryResult(true, "summary", "in-memory"),
            };
            var pipeline = new SummarizerPipeline(
                new InMemoryYtDlpService(),
                Parser(),
                new[] { provider });

            var command = new SummarizeCommand(
                Url: string.Empty,
                PromptStyle.Neutral,
                OutputFormat.PlainText,
                Transcript: new Transcript("abc", "en", new[]
                {
                    new SubtitleSegment(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(7), "Hi"),
                }),
                Timestamps: true,
                SaveTranscriptPath: target);

            var result = await pipeline.RunAsync(command, cancellationToken: CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("summary", result.Content);
            Assert.Equal("[00:00:05] Hi", await File.ReadAllTextAsync(target));
        }
        finally
        {
            File.Delete(target);
        }
    }
    [Fact]
    public async Task RunAsync_TranscriptOnlyProvider_ReturnsFullTextWithoutChunking()
    {
        var transcript = LongTranscript();
        var pipeline = new SummarizerPipeline(
            new InMemoryYtDlpService(),
            Parser(),
            new ILlmProvider[] { new TranscriptOnlyLlmProvider() },
            chunkTokens: 20);

        var command = new SummarizeCommand(
            Url: string.Empty,
            PromptStyle.Neutral,
            OutputFormat.PlainText,
            Transcript: transcript);

        var result = await pipeline.RunAsync(command, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(transcript.ToPlainText(), result.Content);
    }

    private static Transcript LongTranscript() =>
        new("abc", "en", Enumerable.Range(0, 12)
            .Select(i => new SubtitleSegment(
                TimeSpan.FromSeconds(i),
                TimeSpan.FromSeconds(i + 1),
                "alpha beta gamma delta"))
            .ToArray());
    /// <summary>
    /// Synchronous <see cref="IProgress{T}"/>: <see cref="Progress{T}"/> posts to the
    /// thread pool and reorders callbacks, which makes stage-order assertions flaky.
    /// </summary>
    private sealed class InlineProgress : IProgress<PipelineStage>
    {
        private readonly List<PipelineStage> _stages;

        public InlineProgress(List<PipelineStage> stages) => _stages = stages;

        public void Report(PipelineStage value) => _stages.Add(value);
    }
}
