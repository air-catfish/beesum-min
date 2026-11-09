using System.Text.Json;
using Beesum.Core.Abstractions;

namespace Beesum.Core.Storage;

/// <summary>
/// <see cref="ISettingsStore"/> backed by a single JSON file with atomic writes.
/// </summary>
internal sealed class FileSettingsStore : ISettingsStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<string, JsonElement>? _cache;

    /// <summary>Creates a store rooted at <paramref name="filePath"/>.</summary>
    public FileSettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dict = await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!dict.TryGetValue(key, out var element) ||
                element.ValueKind == JsonValueKind.Null)
            {
                return default;
            }

            try
            {
                return element.Deserialize<T>();
            }
            catch (JsonException)
            {
                return default;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dict = await LoadAsync(cancellationToken).ConfigureAwait(false);
            dict[key] = JsonSerializer.SerializeToElement(value);
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

    private async Task<Dictionary<string, JsonElement>> LoadAsync(CancellationToken cancellationToken)
    {
        if (_cache is not null) return _cache;
        if (!File.Exists(_filePath))
        {
            _cache = new();
            return _cache;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            _cache = await JsonSerializer
                .DeserializeAsync<Dictionary<string, JsonElement>>(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false)
                ?? new();
        }
        catch (JsonException) { _cache = new(); }
        catch (IOException) { _cache = new(); }

        return _cache;
    }

    private async Task SaveAsync(Dictionary<string, JsonElement> dict, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
            StoragePermissions.PrivateDirectory(dir);
        }

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer
                .SerializeAsync(stream, dict, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        StoragePermissions.PrivateFile(tempPath);
        File.Move(tempPath, _filePath, overwrite: true);
    }
}
