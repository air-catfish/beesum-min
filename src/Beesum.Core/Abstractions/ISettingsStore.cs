namespace Beesum.Core.Abstractions;

/// <summary>Key/value store for non-secret user settings, persisted as JSON.</summary>
public interface ISettingsStore
{
    /// <summary>Reads a setting, or <c>default</c> when absent.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>Writes a setting.</summary>
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);

    /// <summary>Removes a setting.</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
