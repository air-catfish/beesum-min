namespace Beesum.Core.YtDlp;

/// <summary>Resolved yt-dlp executable path together with its provenance.</summary>
/// <param name="Path">Absolute path to the executable.</param>
/// <param name="Source">Where the path came from.</param>
public sealed record YtDlpResolution(string Path, YtDlpSource Source);
