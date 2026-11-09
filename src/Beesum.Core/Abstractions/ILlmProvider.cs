using Beesum.Core.Models;

namespace Beesum.Core.Abstractions;

/// <summary>A large-language-model backend capable of summarizing a transcript.</summary>
public interface ILlmProvider
{
    /// <summary>Stable machine identifier (e.g. <c>ollama</c>, <c>openai</c>).</summary>
    string Id { get; }

    /// <summary>Human-readable provider name shown in the UI.</summary>
    string DisplayName { get; }

    /// <summary>Probes whether the provider is reachable and ready to serve requests.</summary>
    Task<LlmAvailability> ProbeAsync(CancellationToken cancellationToken = default);

    /// <summary>Summarizes the transcript carried by <paramref name="request"/>.</summary>
    Task<SummaryResult> SummarizeAsync(
        SummaryRequest request,
        CancellationToken cancellationToken = default);
}
