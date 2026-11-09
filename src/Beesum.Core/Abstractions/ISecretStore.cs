namespace Beesum.Core.Abstractions;

/// <summary>
/// Store for API keys. Windows uses DPAPI; Unix uses a user-only (0o600) file
/// because libsecret/Keychain support is not implemented yet.
/// </summary>
public interface ISecretStore
{
    /// <summary>Reads a secret, or <c>null</c> when absent.</summary>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Writes a secret.</summary>
    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>Removes a secret.</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
