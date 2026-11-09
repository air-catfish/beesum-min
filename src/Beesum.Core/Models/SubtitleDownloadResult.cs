namespace Beesum.Core.Models;

/// <summary>Result of a subtitle download attempt via yt-dlp.</summary>
/// <param name="IsSuccess">Whether a subtitle file was produced.</param>
/// <param name="FilePath">Absolute path to the downloaded <c>.vtt</c>, or <c>null</c> on failure.</param>
/// <param name="Language">Subtitle language tag, if known.</param>
/// <param name="ErrorMessage">Failure reason, or <c>null</c> on success.</param>
public sealed record SubtitleDownloadResult(
    bool IsSuccess,
    string? FilePath = null,
    string? Language = null,
    string? ErrorMessage = null);
