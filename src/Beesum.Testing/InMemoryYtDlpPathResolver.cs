using Beesum.Core.Abstractions;
using Beesum.Core.YtDlp;

namespace Beesum.Testing;

/// <summary>Configurable <see cref="IYtDlpPathResolver"/> for tests.</summary>
public sealed class InMemoryYtDlpPathResolver : IYtDlpPathResolver
{
    /// <summary>Resolution returned by <see cref="ResolveAsync"/>.</summary>
    public YtDlpResolution? Result { get; set; }

    /// <summary>Number of calls to <see cref="ResolveAsync"/>.</summary>
    public int ResolveCallCount { get; private set; }

    /// <inheritdoc />
    public Task<YtDlpResolution?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ResolveCallCount++;
        return Task.FromResult(Result);
    }
}
