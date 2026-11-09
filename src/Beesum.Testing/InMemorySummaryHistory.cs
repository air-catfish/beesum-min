using Beesum.Core.Abstractions;
using Beesum.Core.Models;

namespace Beesum.Testing;

/// <summary>In-memory <see cref="ISummaryHistory"/> for tests.</summary>
public sealed class InMemorySummaryHistory : ISummaryHistory
{
    private readonly List<SummaryHistoryEntry> _entries = new();
    private readonly object _gate = new();

    /// <summary>Current entry count, exposed for assertions.</summary>
    public int Count
    {
        get { lock (_gate) { return _entries.Count; } }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SummaryHistoryEntry>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<SummaryHistoryEntry>>(_entries.ToList());
        }
    }

    /// <inheritdoc />
    public Task AddAsync(SummaryHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _entries.Insert(0, entry);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _entries.RemoveAll(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
        }

        return Task.CompletedTask;
    }
}
