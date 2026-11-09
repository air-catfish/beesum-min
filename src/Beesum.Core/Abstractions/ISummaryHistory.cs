using Beesum.Core.Models;

namespace Beesum.Core.Abstractions;

/// <summary>Persistent history of generated summaries.</summary>
public interface ISummaryHistory
{
    /// <summary>Returns all history entries, newest first.</summary>
    Task<IReadOnlyList<SummaryHistoryEntry>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Appends an entry to the history.</summary>
    Task AddAsync(SummaryHistoryEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Removes an entry by id.</summary>
    Task RemoveAsync(string id, CancellationToken cancellationToken = default);
}
