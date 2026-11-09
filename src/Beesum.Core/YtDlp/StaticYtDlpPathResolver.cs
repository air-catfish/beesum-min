using Beesum.Core.Abstractions;

namespace Beesum.Core.YtDlp;

/// <summary>
/// <see cref="IYtDlpPathResolver"/> that returns a fixed path.
/// Used when the host overrides the path explicitly (tests, CLI <c>--yt-dlp</c>).
/// </summary>
internal sealed class StaticYtDlpPathResolver : IYtDlpPathResolver
{
    private readonly YtDlpResolution _resolution;

    /// <summary>Creates a resolver returning <paramref name="path"/> as <see cref="YtDlpSource.Explicit"/>.</summary>
    public StaticYtDlpPathResolver(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _resolution = new YtDlpResolution(path, YtDlpSource.Explicit);
    }

    /// <inheritdoc />
    public Task<YtDlpResolution?> ResolveAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<YtDlpResolution?>(_resolution);
}
