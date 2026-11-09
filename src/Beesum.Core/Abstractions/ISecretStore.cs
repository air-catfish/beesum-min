namespace Beesum.Core.Abstractions;

/// <summary>Store for secrets (API keys), backed by the OS keychain / DPAPI.</summary>
public interface ISecretStore
{
    /// <summary>Reads a secret, or <c>null</c> when absent.</summary>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Writes a secret.</summary>
    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>Removes a secret.</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
