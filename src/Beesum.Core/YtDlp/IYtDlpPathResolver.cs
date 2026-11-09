using Beesum.Core.YtDlp;

namespace Beesum.Core.Abstractions;

/// <summary>
/// Resolves a yt-dlp executable path, honoring user settings first, then the
/// managed copy under <c>tools/</c>, then the system <c>PATH</c>.
/// </summary>
public interface IYtDlpPathResolver
{
    /// <summary>Returns the resolved path and its source, or <c>null</c> if yt-dlp is not found anywhere.</summary>
    Task<YtDlpResolution?> ResolveAsync(CancellationToken cancellationToken = default);
}
