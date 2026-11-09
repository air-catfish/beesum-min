using System.Collections.Concurrent;
using Beesum.Core.Abstractions;

namespace Beesum.Testing;

/// <summary>In-memory <see cref="ISettingsStore"/> for tests.</summary>
public sealed class InMemorySettingsStore : ISettingsStore
{
    private readonly ConcurrentDictionary<string, object?> _values = new(StringComparer.Ordinal);

    /// <summary>Current values, exposed for assertions.</summary>
    public IReadOnlyDictionary<string, object?> Values => _values;

    /// <inheritdoc />
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            _values.TryGetValue(key, out var value) && value is T typed ? typed : default);
    }

    /// <inheritdoc />
    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _values[key] = value;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _values.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
