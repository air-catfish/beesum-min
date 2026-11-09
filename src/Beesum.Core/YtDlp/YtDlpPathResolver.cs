using Beesum.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Beesum.Core.YtDlp;

/// <inheritdoc cref="IYtDlpPathResolver" />
internal sealed class YtDlpPathResolver : IYtDlpPathResolver
{
    /// <summary>Settings key under which an explicit yt-dlp path is stored.</summary>
    public const string SettingKey = "ytdlp.path";

    private readonly ISettingsStore _settings;
    private readonly string _managedPath;
    private readonly string _exeName;
    private readonly string? _searchPath;
    private readonly ILogger<YtDlpPathResolver> _logger;

    /// <summary>Creates a resolver.</summary>
    /// <param name="settings">Settings store, queried for the explicit override.</param>
    /// <param name="managedDirectory">Optional managed tools directory; defaults to <c>{BaseDirectory}/tools</c>.</param>
    /// <param name="logger">Optional logger; <c>null</c> uses a no-op logger.</param>
    /// <param name="searchPath">Optional PATH override; <c>null</c> reads the process environment.</param>
    public YtDlpPathResolver(
        ISettingsStore settings,
        string? managedDirectory = null,
        ILogger<YtDlpPathResolver>? logger = null,
        string? searchPath = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
        _exeName = OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp";
        _managedPath = Path.Combine(
            managedDirectory ?? Path.Combine(AppContext.BaseDirectory, "tools"),
            _exeName);
        _searchPath = searchPath ?? Environment.GetEnvironmentVariable("PATH");
        _logger = logger ?? NullLogger<YtDlpPathResolver>.Instance;
    }

    /// <inheritdoc />
    public async Task<YtDlpResolution?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        // 1. Explicit override from settings
        var explicitPath = await _settings
            .GetAsync<string>(SettingKey, cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
        {
            _logger.LogDebug("yt-dlp resolved from an explicit setting.");
            return new YtDlpResolution(explicitPath, YtDlpSource.Explicit);
        }

        // 2. Managed copy next to the executable
        if (File.Exists(_managedPath))
        {
            _logger.LogDebug("yt-dlp resolved from the managed tools directory.");
            return new YtDlpResolution(_managedPath, YtDlpSource.Managed);
        }

        // 3. PATH lookup
        var path = FindOnPath(_exeName, _searchPath);
        if (path is not null)
        {
            _logger.LogDebug("yt-dlp resolved from PATH.");
            return new YtDlpResolution(path, YtDlpSource.Path);
        }

        _logger.LogDebug("yt-dlp was not found.");
        return null;
    }

    /// <summary>
    /// Scans PATH without spawning <c>which</c>/<c>where</c>: minimal Linux images
    /// often have neither. Returns the first executable match.
    /// </summary>
    internal static string? FindOnPath(string exeName, string? pathVariable)
    {
        if (string.IsNullOrWhiteSpace(pathVariable))
            return null;

        foreach (var raw in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = raw.Trim().Trim('"');
            if (directory.Length == 0)
                continue;

            var candidate = Path.Combine(directory, exeName);
            if (!File.Exists(candidate))
                continue;

            if (OperatingSystem.IsWindows() || IsExecutable(candidate))
                return candidate;
        }

        return null;
    }

    private static bool IsExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
            return true;

        try
        {
            var mode = File.GetUnixFileMode(path);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }
}
