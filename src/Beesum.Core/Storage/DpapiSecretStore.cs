using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Beesum.Core.Abstractions;

namespace Beesum.Core.Storage;

/// <summary>
/// <see cref="ISecretStore"/> encrypted with Windows DPAPI on Windows.
/// <para>
/// On non-Windows platforms the file is written as plain JSON with <c>0o600</c> permissions;
/// replacing this with libsecret / macOS Keychain is planned for v0.2.
/// </para>
/// </summary>
internal sealed class DpapiSecretStore : ISecretStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<string, string>? _cache;

    /// <summary>Creates a store rooted at <paramref name="filePath"/>.</summary>
    public DpapiSecretStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dict = await LoadAsync(cancellationToken).ConfigureAwait(false);
            return dict.TryGetValue(key, out var value) ? value : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dict = await LoadAsync(cancellationToken).ConfigureAwait(false);
            dict[key] = value;
            await SaveAsync(dict, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dict = await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (dict.Remove(key))
                await SaveAsync(dict, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<Dictionary<string, string>> LoadAsync(CancellationToken cancellationToken)
    {
        if (_cache is not null) return _cache;
        if (!File.Exists(_filePath))
        {
            _cache = new();
            return _cache;
        }

        try
        {
            var bytes = await File
                .ReadAllBytesAsync(_filePath, cancellationToken)
                .ConfigureAwait(false);

            var plain = OperatingSystem.IsWindows()
                ? ProtectedData.Unprotect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser)
                : bytes;

            var json = Encoding.UTF8.GetString(plain);
            _cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        catch (JsonException) { _cache = new(); }
        catch (CryptographicException) { _cache = new(); }
        catch (IOException) { _cache = new(); }

        return _cache;
    }

    private async Task SaveAsync(Dictionary<string, string> dict, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
            StoragePermissions.PrivateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(dict);
        var plain = Encoding.UTF8.GetBytes(json);
        var bytes = OperatingSystem.IsWindows()
            ? ProtectedData.Protect(plain, optionalEntropy: null, DataProtectionScope.CurrentUser)
            : plain;

        var tempPath = _filePath + ".tmp";
        await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken).ConfigureAwait(false);

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(
                    tempPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (PlatformNotSupportedException) { /* not reachable in practice */ }
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }
}
