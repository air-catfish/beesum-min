using System.Collections.Concurrent;
using Beesum.Core.Abstractions;
using Beesum.Core.Models;

namespace Beesum.Testing;

/// <summary>In-memory <see cref="ITranscriptCache"/> for tests.</summary>
public sealed class InMemoryTranscriptCache : ITranscriptCache
{
    private readonly ConcurrentDictionary<(string SourceId, string Language), Transcript> _items = new();

    /// <summary>Cached transcripts, exposed for assertions.</summary>
    public IReadOnlyCollection<Transcript> Items => _items.Values.ToList();

    /// <inheritdoc />
    public Task<Transcript?> GetAsync(
        string sourceId,
        string language,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            _items.TryGetValue((sourceId, language), out var transcript) ? transcript : null);
    }

    /// <inheritdoc />
    public Task SetAsync(Transcript transcript, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        cancellationToken.ThrowIfCancellationRequested();
        _items[(transcript.SourceId, transcript.Language)] = transcript;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(
        string sourceId,
        string language,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _items.TryRemove((sourceId, language), out _);
        return Task.CompletedTask;
    }
}
