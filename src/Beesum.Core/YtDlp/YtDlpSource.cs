namespace Beesum.Core.YtDlp;

/// <summary>Where the resolved yt-dlp executable came from.</summary>
public enum YtDlpSource
{
    /// <summary>Explicitly configured path in settings.</summary>
    Explicit,
    /// <summary>Managed copy in <c>tools/</c> next to the executable.</summary>
    Managed,
    /// <summary>Found in <c>PATH</c> via <c>where</c>/<c>which</c>.</summary>
    Path
}
