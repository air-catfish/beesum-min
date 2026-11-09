using Beesum.Core.Models;

namespace Beesum.Core.Abstractions;

/// <summary>End-to-end pipeline: download subtitles, parse, summarize.</summary>
public interface ISummarizerPipeline
{
    /// <summary>Runs the pipeline for <paramref name="command"/>.</summary>
    /// <param name="command">What to summarize.</param>
    /// <param name="progress">Optional progress sink; stages fire in order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SummaryResult> RunAsync(
        SummarizeCommand command,
        IProgress<PipelineStage>? progress = null,
        CancellationToken cancellationToken = default);
}
