using System.Diagnostics;
using Beesum.Core.Abstractions;
using Beesum.Core.Llm;
using Beesum.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Beesum.Core.Pipeline;

/// <inheritdoc cref="ISummarizerPipeline" />
internal sealed class SummarizerPipeline : ISummarizerPipeline
{
    private readonly IYtDlpService _ytDlp;
    private readonly ISubtitleParser _parser;
    private readonly IReadOnlyList<ILlmProvider> _providers;
    private readonly string _providerId;
    private readonly string? _subtitlesDirectory;
    private readonly ILogger<SummarizerPipeline> _logger;
    private readonly bool _chunkingEnabled;
    private readonly int _chunkTokens;

    /// <summary>
    /// Creates a pipeline that downloads subtitles (unless a local <c>.vtt</c> is given),
    /// parses them, and summarizes the transcript with the provider registered under
    /// <paramref name="providerId"/>.
    /// </summary>
    /// <param name="ytDlp">Subtitle download service.</param>
    /// <param name="parser">WebVTT parser.</param>
    /// <param name="providers">Registered LLM providers.</param>
    /// <param name="providerId">Id of the provider to use (e.g. <c>transcript-only</c>, <c>ollama</c>).</param>
    /// <param name="subtitlesDirectory">Optional persistent directory for downloaded <c>.vtt</c> files; when <c>null</c>, a temp directory is used and cleaned up.</param>
    /// <param name="logger">Optional logger; <c>null</c> uses a no-op logger.</param>
    /// <param name="chunkingEnabled">Whether long transcripts are split into chunks.</param>
    /// <param name="chunkTokens">Approximate input budget per chunk.</param>
    public SummarizerPipeline(
        IYtDlpService ytDlp,
        ISubtitleParser parser,
        IEnumerable<ILlmProvider> providers,
        string providerId = "transcript-only",
        string? subtitlesDirectory = null,
        ILogger<SummarizerPipeline>? logger = null,
        bool chunkingEnabled = true,
        int chunkTokens = 6000)
    {
        ArgumentNullException.ThrowIfNull(ytDlp);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);

        _ytDlp = ytDlp;
        _parser = parser;
        _providers = providers.ToList();
        _providerId = providerId;
        _subtitlesDirectory = subtitlesDirectory;
        _logger = logger ?? NullLogger<SummarizerPipeline>.Instance;
        _chunkingEnabled = chunkingEnabled;
        _chunkTokens = chunkTokens;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_chunkTokens, nameof(chunkTokens));
    }

    /// <inheritdoc />
    public async Task<SummaryResult> RunAsync(
        SummarizeCommand command,
        IProgress<PipelineStage>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report(PipelineStage.Starting);
        _logger.LogDebug("Pipeline started with provider {ProviderId}.", _providerId);

        var provider = _providers.FirstOrDefault(p =>
            string.Equals(p.Id, _providerId, StringComparison.Ordinal));
        if (provider is null)
        {
            _logger.LogWarning("LLM provider {ProviderId} is not registered.", _providerId);
            return new SummaryResult(
                false,
                string.Empty,
                _providerId,
                $"LLM provider '{_providerId}' is not registered.");
        }

        if (command.From is not null && command.To is not null && command.From.Value >= command.To.Value)
        {
            return new SummaryResult(
                false,
                string.Empty,
                provider.Id,
                "'from' must be before 'to'.");
        }

        if (command.Transcript is not null)
        {
            var clipped = command.Transcript.Slice(command.From, command.To);
            if ((command.From is not null || command.To is not null) && clipped.Segments.Count == 0)
            {
                return new SummaryResult(
                    false,
                    string.Empty,
                    provider.Id,
                    "No cues in the selected time range.");
            }

            var transcriptSaveError = TrySaveTranscript(
                clipped,
                command.SaveTranscriptPath,
                command.Timestamps);
            if (transcriptSaveError is not null)
            {
                return new SummaryResult(false, string.Empty, provider.Id, transcriptSaveError);
            }

            return await SummarizeAsync(
                provider,
                clipped,
                command,
                progress,
                cancellationToken).ConfigureAwait(false);
        }

        var createdTempDir = false;
        string? tempDir = null;

        try
        {
            string vttPath;
            string? language = null;
            string sourceId;

            if (!string.IsNullOrWhiteSpace(command.LocalVttPath))
            {
                if (!File.Exists(command.LocalVttPath))
                {
                    return new SummaryResult(
                        false,
                        string.Empty,
                        null,
                        $"Local .vtt not found: {command.LocalVttPath}");
                }

                vttPath = command.LocalVttPath;
                sourceId = DeriveSourceId(vttPath);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(command.Url))
                {
                    return new SummaryResult(
                        false,
                        string.Empty,
                        null,
                        "URL is required when no local .vtt is provided.");
                }

                progress?.Report(PipelineStage.Downloading);

                string workDir;
                try
                {
                    var work = CreateWorkDirectory();
                    workDir = work.Path;
                    tempDir = work.IsTemporary ? work.Path : null;
                    createdTempDir = work.IsTemporary;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return new SummaryResult(
                        false,
                        string.Empty,
                        null,
                        "Cannot create a working directory for subtitles. " +
                        "Check that %TEMP% is writable (antivirus or group policy can block it) " +
                        $"and try again. Details: {ex.Message}");
                }

                var download = await _ytDlp
                    .DownloadSubtitlesAsync(command.Url, workDir, command.SubtitleLanguages, cancellationToken)
                    .ConfigureAwait(false);

                if (!download.IsSuccess || string.IsNullOrEmpty(download.FilePath))
                {
                    return new SummaryResult(
                        false,
                        string.Empty,
                        null,
                        download.ErrorMessage ?? "Subtitle download failed.");
                }

                vttPath = download.FilePath;
                language = download.Language;
                sourceId = DeriveSourceId(vttPath);
            }

            if (!string.IsNullOrWhiteSpace(command.SaveVttPath))
            {
                var saveError = TrySaveVtt(vttPath, command.SaveVttPath);
                if (saveError is not null)
                {
                    return new SummaryResult(false, string.Empty, provider.Id, saveError);
                }
            }

            progress?.Report(PipelineStage.Parsing);
            var transcript = await _parser
                .ParseFileAsync(vttPath, sourceId, language, cancellationToken)
                .ConfigureAwait(false);

            var clipped = transcript.Slice(command.From, command.To);
            if ((command.From is not null || command.To is not null) && clipped.Segments.Count == 0)
            {
                return new SummaryResult(
                    false,
                    string.Empty,
                    provider.Id,
                    "No cues in the selected time range.");
            }

            var transcriptSaveError = TrySaveTranscript(
                clipped,
                command.SaveTranscriptPath,
                command.Timestamps);
            if (transcriptSaveError is not null)
            {
                return new SummaryResult(false, string.Empty, provider.Id, transcriptSaveError);
            }

            return await SummarizeAsync(
                provider,
                clipped,
                command,
                progress,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (createdTempDir && tempDir is not null)
            {
                try
                {
                    Directory.Delete(tempDir, recursive: true);
                }
                catch (IOException) { /* best effort */ }
                catch (UnauthorizedAccessException) { /* best effort */ }
            }
        }
    }

    private async Task<SummaryResult> SummarizeAsync(
        ILlmProvider provider,
        Transcript transcript,
        SummarizeCommand command,
        IProgress<PipelineStage>? progress,
        CancellationToken cancellationToken)
    {
        if (transcript.Segments.Count == 0 || string.IsNullOrWhiteSpace(transcript.ToPlainText()))
        {
            return new SummaryResult(
                false,
                string.Empty,
                provider.Id,
                "No subtitle text found.");
        }

        // transcript-only just hands the text back, so splitting it into chunks and
        // "combining" the parts would only add calls without changing the result.
        var result = _chunkingEnabled && !command.NoChunk &&
                     provider is not TranscriptOnlyLlmProvider &&
                     TokenEstimator.Estimate(transcript) > _chunkTokens
            ? await SummarizeChunkedAsync(
                provider,
                transcript,
                command,
                progress,
                cancellationToken).ConfigureAwait(false)
            : await SummarizeSingleAsync(
                provider,
                transcript,
                command,
                progress,
                cancellationToken).ConfigureAwait(false);

        if (command.Format == OutputFormat.MindMap &&
            result.IsSuccess &&
            !string.Equals(provider.Id, "transcript-only", StringComparison.Ordinal))
        {
            result = result with { Content = Beesum.Core.Formatting.MindMapFormatter.Normalize(result.Content) };
        }

        return result;
    }

    private async Task<SummaryResult> SummarizeChunkedAsync(
        ILlmProvider provider,
        Transcript transcript,
        SummarizeCommand command,
        IProgress<PipelineStage>? progress,
        CancellationToken cancellationToken)
    {
        var chunks = TranscriptChunker.Split(transcript, _chunkTokens);
        if (chunks.Count <= 1)
        {
            return await SummarizeSingleAsync(
                provider,
                transcript,
                command,
                progress,
                cancellationToken).ConfigureAwait(false);
        }

        progress?.Report(PipelineStage.Chunking);
        _logger.LogInformation("Transcript split into {ChunkCount} chunks.", chunks.Count);

        var stopwatch = Stopwatch.StartNew();
        var partials = new List<string>(chunks.Count);
        var usages = new List<TokenUsage>();
        string? modelName = null;
        var anyTruncated = false;

        for (var index = 0; index < chunks.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Summarizing chunk {Index}/{Count}.", index + 1, chunks.Count);

            var mapRequest = new SummaryRequest(
                chunks[index],
                command.Style,
                command.Format,
                $"This is part {index + 1} of {chunks.Count} of a transcript. " +
                "Summarize only this part: keep names, numbers, decisions, and conclusions. " +
                "The partial summaries will be combined later.",
                command.SystemPrompt,
                command.Timestamps);

            var partial = await provider.SummarizeAsync(mapRequest, cancellationToken).ConfigureAwait(false);
            if (!partial.IsSuccess)
            {
                _logger.LogWarning(
                    "Chunk {Index}/{Count} failed: {ErrorMessage}",
                    index + 1,
                    chunks.Count,
                    partial.ErrorMessage);

                return new SummaryResult(
                    false,
                    string.Empty,
                    partial.ProviderId ?? _providerId,
                    $"Chunk {index + 1}/{chunks.Count} failed: {partial.ErrorMessage}");
            }

            partials.Add(partial.Content);
            if (partial.Usage is not null) usages.Add(partial.Usage);
            modelName ??= partial.ModelName;
            anyTruncated |= partial.Truncated;
        }

        var final = await ReduceAsync(
            provider,
            partials,
            command,
            transcript.SourceId,
            transcript.Language,
            usages,
            depth: 0,
            cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();

        if (!final.IsSuccess)
            return final;

        progress?.Report(PipelineStage.Completed);
        _logger.LogInformation(
            "Chunked summary completed with provider {ProviderId} in {ElapsedMs} ms.",
            final.ProviderId,
            stopwatch.ElapsedMilliseconds);

        return final with
        {
            ModelName = final.ModelName ?? modelName,
            Usage = SumUsage(usages),
            Duration = stopwatch.Elapsed,
            Truncated = final.Truncated || anyTruncated,
        };
    }

    private async Task<SummaryResult> ReduceAsync(
        ILlmProvider provider,
        IReadOnlyList<string> partials,
        SummarizeCommand command,
        string sourceId,
        string language,
        List<TokenUsage> usages,
        int depth,
        CancellationToken cancellationToken)
    {
        var combinedText = string.Join("\n\n", partials);

        if (depth < 3 && partials.Count > 1 && TokenEstimator.Estimate(combinedText) > _chunkTokens)
        {
            _logger.LogDebug("Reducing {Count} partial summaries at depth {Depth}.", partials.Count, depth);

            var reduced = new List<string>();
            foreach (var group in GroupPartials(partials))
            {
                var groupRequest = new SummaryRequest(
                    TranscriptFromText(string.Join("\n\n", group), sourceId, language),
                    command.Style,
                    command.Format,
                    "Combine these partial summaries into a shorter intermediate summary. Keep facts and numbers.",
                    command.SystemPrompt,
                    Timestamps: false);

                var groupResult = await provider.SummarizeAsync(groupRequest, cancellationToken).ConfigureAwait(false);
                if (!groupResult.IsSuccess)
                    return groupResult;

                if (groupResult.Usage is not null) usages.Add(groupResult.Usage);
                reduced.Add(groupResult.Content);
            }

            return await ReduceAsync(
                provider,
                reduced,
                command,
                sourceId,
                language,
                usages,
                depth + 1,
                cancellationToken).ConfigureAwait(false);
        }

        var instruction = string.IsNullOrWhiteSpace(command.Instructions)
            ? "The input is a set of partial summaries of one transcript. Combine them into one coherent summary."
            : command.Instructions +
              "\n\nThe input is a set of partial summaries of one transcript. Combine them into one coherent summary.";

        var finalRequest = new SummaryRequest(
            TranscriptFromText(combinedText, sourceId, language),
            command.Style,
            command.Format,
            instruction,
            command.SystemPrompt,
            Timestamps: false);

        var final = await provider.SummarizeAsync(finalRequest, cancellationToken).ConfigureAwait(false);
        if (final.Usage is not null) usages.Add(final.Usage);
        return final;
    }

    private List<List<string>> GroupPartials(IReadOnlyList<string> partials)
    {
        var groups = new List<List<string>>();
        var current = new List<string>();
        var tokens = 0;

        foreach (var partial in partials)
        {
            var estimate = TokenEstimator.Estimate(partial);
            if (current.Count > 0 && tokens + estimate > _chunkTokens)
            {
                groups.Add(current);
                current = new List<string>();
                tokens = 0;
            }

            current.Add(partial);
            tokens += estimate;
        }

        if (current.Count > 0) groups.Add(current);
        return groups;
    }

    private static Transcript TranscriptFromText(string text, string sourceId, string language) =>
        new(sourceId, language, new[]
        {
            new SubtitleSegment(TimeSpan.Zero, TimeSpan.Zero, text),
        });

    private static TokenUsage? SumUsage(IEnumerable<TokenUsage> usages)
    {
        TokenUsage? total = null;
        foreach (var usage in usages)
        {
            total = total is null
                ? usage
                : new TokenUsage(
                    total.PromptTokens + usage.PromptTokens,
                    total.CompletionTokens + usage.CompletionTokens,
                    total.TotalTokens + usage.TotalTokens);
        }

        return total;
    }
    private async Task<SummaryResult> SummarizeSingleAsync(
        ILlmProvider provider,
        Transcript transcript,
        SummarizeCommand command,
        IProgress<PipelineStage>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(PipelineStage.Summarizing);

        var request = new SummaryRequest(
            transcript,
            command.Style,
            command.Format,
            command.Instructions,
            command.SystemPrompt,
            command.Timestamps);

        var stopwatch = Stopwatch.StartNew();
        var result = await provider.SummarizeAsync(request, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        if (result.IsSuccess && result.Duration is null)
            result = result with { Duration = stopwatch.Elapsed };

        if (result.IsSuccess)
        {
            progress?.Report(PipelineStage.Completed);
            _logger.LogInformation(
                "Summary completed with provider {ProviderId} in {ElapsedMs} ms.",
                result.ProviderId,
                stopwatch.ElapsedMilliseconds);
        }
        else
        {
            _logger.LogWarning(
                "Summarization failed with provider {ProviderId}: {ErrorMessage}",
                result.ProviderId,
                result.ErrorMessage);
        }

        return result;
    }

    private static string? TrySaveTranscript(Transcript transcript, string? targetPath, bool timestamps)
    {
        if (string.IsNullOrWhiteSpace(targetPath)) return null;
        if (transcript.Segments.Count == 0 || string.IsNullOrWhiteSpace(transcript.ToPlainText())) return null;

        try
        {
            var target = Path.GetFullPath(targetPath);
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(target, transcript.ToPlainText(timestamps));
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return $"Could not save transcript to '{targetPath}': {ex.Message}";
        }
    }

    private static string? TrySaveVtt(string sourcePath, string targetPath)
    {
        try
        {
            var source = Path.GetFullPath(sourcePath);
            var target = Path.GetFullPath(targetPath);
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (string.Equals(source, target, comparison)) return null;

            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.Copy(source, target, overwrite: true);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return $"Could not save subtitles to '{targetPath}': {ex.Message}";
        }
    }
    private static string DeriveSourceId(string vttPath)
    {
        var name = Path.GetFileNameWithoutExtension(vttPath);
        var dot = name.IndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }

    /// <summary>
    /// Returns a writable directory for the downloaded subtitles. A configured
    /// <c>subtitlesDirectory</c> wins; otherwise falls back from <c>%TEMP%</c> to
    /// <c>%APPDATA%\Beesum\temp</c> and finally to a directory next to the app,
    /// because antivirus/policies and locked-down profiles can make <c>%TEMP%</c>
    /// unwritable.
    /// </summary>
    private (string Path, bool IsTemporary) CreateWorkDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_subtitlesDirectory) &&
            TryPrepareWritableDirectory(_subtitlesDirectory))
        {
            return (_subtitlesDirectory, false);
        }

        try
        {
            var temp = Directory.CreateTempSubdirectory("beesum-");
            return (temp.FullName, true);
        }
        catch (IOException) { /* fall through */ }
        catch (UnauthorizedAccessException) { /* fall through */ }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            var appDataTemp = Path.Combine(
                appData, "Beesum", "temp", Guid.NewGuid().ToString("N"));
            if (TryPrepareWritableDirectory(appDataTemp))
                return (appDataTemp, true);
        }

        var localTemp = Path.Combine(
            AppContext.BaseDirectory, "temp", Guid.NewGuid().ToString("N"));
        if (TryPrepareWritableDirectory(localTemp))
            return (localTemp, true);

        throw new IOException(
            "Neither %TEMP%, %APPDATA%\\Beesum\\temp, nor the application directory is writable.");
    }

    private static bool TryPrepareWritableDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, ".beesum-write-probe-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (NotSupportedException) { return false; }
    }
}
