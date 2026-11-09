using System.Text.Json;
using Beesum.Core.Abstractions;
using Beesum.Core.Models;

namespace Beesum.Core.Storage;

/// <summary>
/// <see cref="ISummaryHistory"/> backed by an append-only JSONL file.
/// Corrupt lines are skipped on read; removal rewrites the file.
/// </summary>
internal sealed class JsonSummaryHistory : ISummaryHistory
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>Creates a history store at <paramref name="filePath"/>.</summary>
    public JsonSummaryHistory(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SummaryHistoryEntry>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = await ReadAllAsync(cancellationToken).ConfigureAwait(false);
            entries.Reverse(); // newest first
            return entries;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task AddAsync(SummaryHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var line = JsonSerializer.Serialize(entry) + "\n";
            await File.AppendAllTextAsync(_filePath, line, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var all = await ReadAllAsync(cancellationToken).ConfigureAwait(false);
            var kept = all.Where(e => !string.Equals(e.Id, id, StringComparison.Ordinal)).ToList();
            if (kept.Count == all.Count) return; // nothing removed

            var tempPath = _filePath + ".tmp";
            await using (var stream = File.Create(tempPath))
            await using (var writer = new StreamWriter(stream))
            {
                foreach (var e in kept)
                    await writer.WriteLineAsync(JsonSerializer.Serialize(e)).ConfigureAwait(false);
            }
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<SummaryHistoryEntry>> ReadAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath)) return new();

        var lines = await File
            .ReadAllLinesAsync(_filePath, cancellationToken)
            .ConfigureAwait(false);

        var result = new List<SummaryHistoryEntry>(lines.Length);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var entry = JsonSerializer.Deserialize<SummaryHistoryEntry>(line);
                if (entry is not null) result.Add(entry);
            }
            catch (JsonException) { /* skip corrupt line */ }
        }
        return result;
    }
}
