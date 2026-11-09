using Beesum.Core.Abstractions;
using Beesum.Core.Models;

namespace Beesum.Core.Llm;

/// <summary>
/// Degraded <see cref="ILlmProvider"/> that returns the raw transcript.
/// Useful when no LLM is configured: the pipeline still produces output.
/// </summary>
public sealed class TranscriptOnlyLlmProvider : ILlmProvider
{
    /// <inheritdoc />
    public string Id => "transcript-only";

    /// <inheritdoc />
    public string DisplayName => "Transcript only (no LLM)";

    /// <inheritdoc />
    public Task<LlmAvailability> ProbeAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new LlmAvailability(true, "No LLM required.", "none"));

    /// <inheritdoc />
    public Task<SummaryResult> SummarizeAsync(
        SummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(new SummaryResult(true, request.Transcript.ToPlainText(request.Timestamps), Id));
    }
}
