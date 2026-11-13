using System.Diagnostics.CodeAnalysis;
using Beesum.Core.Models;

namespace Beesum.Core.Abstractions;

/// <summary>Wraps the managed yt-dlp executable: info lookup, updates, subtitle download.</summary>
public interface IYtDlpService
{
    /// <summary>Fetches video metadata for <paramref name="url"/>.</summary>
    [SuppressMessage(
        "Design",
        "CA1054:URI parameters should not be strings",
        Justification = "yt-dlp consumes URLs as plain strings.")]
    Task<VideoInfo?> GetInfoAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>Checks whether a newer yt-dlp release is available.</summary>
    Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs yt-dlp's self-update on the managed copy.</summary>
    Task<bool> UpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>Downloads subtitles for <paramref name="url"/> into <paramref name="outputDirectory"/>.</summary>
    [SuppressMessage(
        "Design",
        "CA1054:URI parameters should not be strings",
        Justification = "yt-dlp consumes URLs as plain strings.")]
    Task<SubtitleDownloadResult> DownloadSubtitlesAsync(
        string url,
        string outputDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads subtitles in the given language selectors (for example <c>ru</c> or
    /// <c>ru,en</c>) instead of the configured defaults. Implementations that do not
    /// support per-call languages fall back to the three-argument overload.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1054:URI parameters should not be strings",
        Justification = "yt-dlp consumes URLs as plain strings.")]
    Task<SubtitleDownloadResult> DownloadSubtitlesAsync(
        string url,
        string outputDirectory,
        IReadOnlyList<string>? languages,
        CancellationToken cancellationToken = default) =>
        DownloadSubtitlesAsync(url, outputDirectory, cancellationToken);
}
