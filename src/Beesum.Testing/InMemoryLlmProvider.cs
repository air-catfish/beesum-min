using Beesum.Core.Abstractions;
using Beesum.Core.Models;

namespace Beesum.Testing;

/// <summary>Configurable <see cref="ILlmProvider"/> for tests.</summary>
public sealed class InMemoryLlmProvider : ILlmProvider
{
    /// <summary>Creates a provider.</summary>
    /// <param name="id">Provider id returned in results and used for selection.</param>
    /// <param name="displayName">Human-readable name.</param>
    public InMemoryLlmProvider(string id = "transcript-only", string displayName = "In-memory LLM")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
        DisplayName = displayName;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <summary>Availability returned by <see cref="ProbeAsync"/>.</summary>
    public LlmAvailability Availability { get; set; } = new(true);

    /// <summary>Result returned by <see cref="SummarizeAsync"/>.</summary>
    public SummaryResult Result { get; set; } = new(true, "summary", "in-memory");

    private readonly Queue<SummaryResult> _responses = new();

    /// <summary>Requests received by <see cref="SummarizeAsync"/>.</summary>
    public List<SummaryRequest> Requests { get; } = new();

    /// <summary>Queues a result for the next call; when empty, <see cref="Result"/> is used.</summary>
    public InMemoryLlmProvider Enqueue(SummaryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _responses.Enqueue(result);
        return this;
    }

    /// <summary>Number of calls to <see cref="SummarizeAsync"/>.</summary>
    public int SummarizeCallCount { get; private set; }

    /// <inheritdoc />
    public Task<LlmAvailability> ProbeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Availability);
    }

    /// <inheritdoc />
    public Task<SummaryResult> SummarizeAsync(
        SummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        SummarizeCallCount++;
        Requests.Add(request);
        return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : Result);
    }
}
