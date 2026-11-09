using Beesum.Core.Abstractions;
using Beesum.Core.Models;

namespace Beesum.Testing;

/// <summary>Configurable <see cref="IYtDlpService"/> for tests.</summary>
public sealed class InMemoryYtDlpService : IYtDlpService
{
    /// <summary>Calls to <see cref="DownloadSubtitlesAsync"/>, in order.</summary>
    public List<(string Url, string OutputDirectory)> DownloadCalls { get; } = new();

    /// <summary>Calls to <see cref="GetInfoAsync"/>, in order.</summary>
    public List<string> GetInfoCalls { get; } = new();

    /// <summary>Result returned by <see cref="DownloadSubtitlesAsync"/>.</summary>
    public SubtitleDownloadResult DownloadResult { get; set; } =
        new(false, ErrorMessage: "not configured");

    /// <summary>Result returned by <see cref="GetInfoAsync"/>.</summary>
    public VideoInfo? InfoResult { get; set; }

    /// <summary>Result returned by <see cref="CheckForUpdatesAsync"/>.</summary>
    public bool CheckForUpdatesResult { get; set; }

    /// <summary>Result returned by <see cref="UpdateAsync"/>.</summary>
    public bool UpdateResult { get; set; }

    /// <inheritdoc />
    public Task<VideoInfo?> GetInfoAsync(string url, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GetInfoCalls.Add(url);
        return Task.FromResult(InfoResult);
    }

    /// <inheritdoc />
    public Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CheckForUpdatesResult);
    }

    /// <inheritdoc />
    public Task<bool> UpdateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(UpdateResult);
    }

    /// <inheritdoc />
    public Task<SubtitleDownloadResult> DownloadSubtitlesAsync(
        string url,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DownloadCalls.Add((url, outputDirectory));
        return Task.FromResult(DownloadResult);
    }
}
