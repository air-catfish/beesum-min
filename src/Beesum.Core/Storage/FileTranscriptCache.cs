using System.Text.Json;
using Beesum.Core.Abstractions;
using Beesum.Core.Models;

namespace Beesum.Core.Storage;

/// <summary>
/// <see cref="ITranscriptCache"/> storing one JSON file per transcript.
/// </summary>
internal sealed class FileTranscriptCache : ITranscriptCache
{
    private readonly string _directory;

    /// <summary>Creates a cache rooted at <paramref name="directory"/>.</summary>
    public FileTranscriptCache(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    /// <inheritdoc />
    public async Task<Transcript?> GetAsync(
        string sourceId,
        string language,
        CancellationToken cancellationToken = default)
    {
        var path = GetPath(sourceId, language);
        if (!File.Exists(path)) return null;

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer
                .DeserializeAsync<Transcript>(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
    }

    /// <inheritdoc />
    public async Task SetAsync(Transcript transcript, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        Directory.CreateDirectory(_directory);
        StoragePermissions.PrivateDirectory(_directory);

        var path = GetPath(transcript.SourceId, transcript.Language);
        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer
                .SerializeAsync(stream, transcript, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        StoragePermissions.PrivateFile(tempPath);
        File.Move(tempPath, path, overwrite: true);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string sourceId, string language, CancellationToken cancellationToken = default)
    {
        var path = GetPath(sourceId, language);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string GetPath(string sourceId, string language)
    {
        var safeId = Sanitize(sourceId);
        var safeLang = Sanitize(language);
        return Path.Combine(_directory, $"{safeId}.{safeLang}.json");
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value)) return "_";
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (!char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-')
                chars[i] = '_';
        }
        return new string(chars);
    }
}
