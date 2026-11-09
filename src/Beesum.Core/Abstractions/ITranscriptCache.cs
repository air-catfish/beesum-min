using Beesum.Core.Models;

namespace Beesum.Core.Abstractions;

/// <summary>Cache of parsed transcripts, keyed by source and language.</summary>
public interface ITranscriptCache
{
    /// <summary>Retrieves a cached transcript, or <c>null</c> on miss.</summary>
    Task<Transcript?> GetAsync(
        string sourceId,
        string language,
        CancellationToken cancellationToken = default);

    /// <summary>Stores a transcript in the cache.</summary>
    Task SetAsync(Transcript transcript, CancellationToken cancellationToken = default);

    /// <summary>Removes a transcript from the cache.</summary>
    Task RemoveAsync(
        string sourceId,
        string language,
        CancellationToken cancellationToken = default);
}
