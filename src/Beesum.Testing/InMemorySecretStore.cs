using System.Collections.Concurrent;
using Beesum.Core.Abstractions;

namespace Beesum.Testing;

/// <summary>In-memory <see cref="ISecretStore"/> for tests.</summary>
public sealed class InMemorySecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <summary>Current secrets, exposed for assertions.</summary>
    public IReadOnlyDictionary<string, string> Values => _values;

    /// <inheritdoc />
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);
    }

    /// <inheritdoc />
    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
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
