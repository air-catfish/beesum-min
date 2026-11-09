using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Beesum.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Beesum.Core.Models;

namespace Beesum.Core.YtDlp;

/// <inheritdoc cref="IYtDlpService" />
internal sealed class YtDlpService : IYtDlpService
{
    private static readonly TimeSpan InfoTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan UpdateTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(5);
    private const string NotFoundMessage =
        "yt-dlp not found. Install it in PATH, place it in tools/ next to the app, or set 'ytdlp.path' in settings.";

    private readonly IProcessRunner _runner;
    private readonly IYtDlpPathResolver _pathResolver;
    private readonly IReadOnlyList<string> _extraArgs;
    private readonly IReadOnlyList<string> _subtitleLanguages;
    private readonly HttpClient? _httpClient;
    private readonly ILogger<YtDlpService> _logger;

    /// <summary>Creates a service backed by a path resolver.</summary>
    /// <param name="runner">Process runner used to invoke yt-dlp.</param>
    /// <param name="pathResolver">Resolver locating the yt-dlp executable.</param>
    /// <param name="httpClient">Optional HTTP client used by <see cref="CheckForUpdatesAsync"/>.</param>
    /// <param name="extraArgs">
    /// Optional extra arguments inserted before the URL (or at the end for non-URL calls),
    /// e.g. <c>--proxy</c> or <c>--cookies-from-browser</c>.
    /// </param>
    /// <param name="logger">Optional logger; <c>null</c> uses a no-op logger.</param>
    /// <param name="subtitleLanguages">Subtitle language selectors for yt-dlp; defaults to <c>en</c>.</param>
    public YtDlpService(
        IProcessRunner runner,
        IYtDlpPathResolver pathResolver,
        HttpClient? httpClient = null,
        IReadOnlyList<string>? extraArgs = null,
        ILogger<YtDlpService>? logger = null,
        IReadOnlyList<string>? subtitleLanguages = null)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(pathResolver);
        _runner = runner;
        _pathResolver = pathResolver;
        _httpClient = httpClient;
        _extraArgs = extraArgs ?? Array.Empty<string>();
        _subtitleLanguages = subtitleLanguages is { Count: > 0 }
            ? subtitleLanguages
            : new[] { "en" };
        _logger = logger ?? NullLogger<YtDlpService>.Instance;
    }

    /// <inheritdoc />
    public async Task<VideoInfo?> GetInfoAsync(
        [SuppressMessage("Design", "CA1054:URI parameters should not be strings",
            Justification = "yt-dlp consumes URLs as plain strings.")]
        string url,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var resolution = await SafeResolveAsync(cancellationToken).ConfigureAwait(false);
        if (resolution is null) return null;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(InfoTimeout);

        var args = new List<string> { "--dump-json", "--no-playlist", "--skip-download" };
        args.AddRange(_extraArgs);
        args.Add(url);

        ProcessResult result;
        try
        {
            result = await _runner.RunAsync(
                resolution.Path,
                args,
                cancellationToken: cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            return null;
        }
        catch (Exception) { return null; }

        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
            return null;

        var firstLine = result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (firstLine is null) return null;

        try
        {
            using var doc = JsonDocument.Parse(firstLine);
            var root = doc.RootElement;

            var id = TryGetString(root, "id");
            var title = TryGetString(root, "title");
            if (id is null || title is null) return null;

            var duration = TimeSpan.Zero;
            if (root.TryGetProperty("duration", out var durProp) &&
                durProp.ValueKind == JsonValueKind.Number &&
                durProp.TryGetDouble(out var seconds))
            {
                duration = TimeSpan.FromSeconds(seconds);
            }

            return new VideoInfo(
                id,
                title,
                duration,
                Author: TryGetString(root, "uploader"),
                ThumbnailUrl: TryGetString(root, "thumbnail"));
        }
        catch (JsonException) { return null; }
    }

    /// <inheritdoc />
    public async Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (_httpClient is null) return false;

        var resolution = await SafeResolveAsync(cancellationToken).ConfigureAwait(false);
        if (resolution is null || resolution.Source != YtDlpSource.Managed)
            return false;

        string localVersion;
        try
        {
            var args = new List<string> { "--version" };
            args.AddRange(_extraArgs);

            var versionResult = await _runner.RunAsync(
                resolution.Path,
                args,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (versionResult.ExitCode != 0) return false;
            localVersion = versionResult.StandardOutput.Trim();
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            return false;
        }
        catch (Exception) { return false; }

        if (string.IsNullOrEmpty(localVersion)) return false;

        string? remoteVersion;
        try
        {
            using var response = await _httpClient
                .GetAsync("https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest", cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return false;
            var body = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            remoteVersion = TryGetString(doc.RootElement, "tag_name");
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            return false;
        }
        catch (Exception) { return false; }

        if (string.IsNullOrEmpty(remoteVersion)) return false;

        return !string.Equals(localVersion, remoteVersion, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(CancellationToken cancellationToken = default)
    {
        var resolution = await SafeResolveAsync(cancellationToken).ConfigureAwait(false);
        if (resolution is null || resolution.Source != YtDlpSource.Managed)
            return false;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(UpdateTimeout);

        try
        {
            var args = new List<string> { "-U" };
            args.AddRange(_extraArgs);

            var result = await _runner.RunAsync(
                resolution.Path,
                args,
                cancellationToken: cts.Token).ConfigureAwait(false);
            return result.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            return false;
        }
        catch (Exception) { return false; }
    }

    /// <inheritdoc />
    public async Task<SubtitleDownloadResult> DownloadSubtitlesAsync(
        [SuppressMessage("Design", "CA1054:URI parameters should not be strings",
            Justification = "yt-dlp consumes URLs as plain strings.")]
        string url,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return new SubtitleDownloadResult(false, ErrorMessage: "URL is empty.");
        if (string.IsNullOrWhiteSpace(outputDirectory))
            return new SubtitleDownloadResult(false, ErrorMessage: "Output directory is empty.");

        var resolution = await SafeResolveAsync(cancellationToken).ConfigureAwait(false);
        if (resolution is null)
        {
            _logger.LogWarning("yt-dlp executable was not resolved.");
            return new SubtitleDownloadResult(false, ErrorMessage: NotFoundMessage);
        }

        Directory.CreateDirectory(outputDirectory);

        var args = new List<string>
        {
            "--write-subs",
            "--write-auto-subs",
            "--sub-format", "vtt",
            "--sub-langs", string.Join(",", _subtitleLanguages),
            "--skip-download",
            "--no-playlist",
            "-o", "%(id)s.%(ext)s",
        };
        args.AddRange(_extraArgs);
        args.Add(url);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(DownloadTimeout);

        ProcessResult result;
        try
        {
            result = await _runner.RunAsync(
                resolution.Path,
                args,
                workingDirectory: outputDirectory,
                cancellationToken: cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            return new SubtitleDownloadResult(false, ErrorMessage: "Subtitle download timed out.");
        }
        catch (Exception ex)
        {
            return new SubtitleDownloadResult(false, ErrorMessage: ex.Message);
        }

        if (result.ExitCode != 0)
        {
            _logger.LogWarning("yt-dlp subtitle download failed with exit code {ExitCode}.", result.ExitCode);
            var err = string.IsNullOrWhiteSpace(result.StandardError)
                ? $"yt-dlp exited with code {result.ExitCode}."
                : result.StandardError.Trim();
            return new SubtitleDownloadResult(false, ErrorMessage: err);
        }

        var vttFiles = Directory
            .EnumerateFiles(outputDirectory, "*.vtt", SearchOption.TopDirectoryOnly)
            .ToList();
        var vttFile = SelectSubtitleFile(vttFiles, _subtitleLanguages);
        if (vttFile is null)
        {
            _logger.LogWarning("yt-dlp produced no .vtt file.");
            return new SubtitleDownloadResult(false, ErrorMessage: "No .vtt file was produced.");
        }

        _logger.LogDebug("Subtitles downloaded from yt-dlp.");
        return new SubtitleDownloadResult(true, vttFile, ExtractLanguage(vttFile));
    }

    private async Task<YtDlpResolution?> SafeResolveAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _pathResolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return null; }
    }

    private static string? TryGetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    internal static string? SelectSubtitleFile(
        IReadOnlyList<string> files,
        IReadOnlyList<string> languages)
    {
        foreach (var language in languages)
        {
            var selector = language.Trim();
            if (selector.Length == 0) continue;

            if (selector.EndsWith(".*", StringComparison.Ordinal))
            {
                var prefix = selector[..^2];
                var match = files.FirstOrDefault(file =>
                    ExtractLanguage(file) is { } actual &&
                    (string.Equals(actual, prefix, StringComparison.OrdinalIgnoreCase) ||
                     actual.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase)));

                if (match is not null) return match;
                continue;
            }

            var exact = files.FirstOrDefault(file =>
                string.Equals(ExtractLanguage(file), selector, StringComparison.OrdinalIgnoreCase));

            if (exact is not null) return exact;
        }

        return files.Count > 0 ? files[0] : null;
    }

    private static string? ExtractLanguage(string vttPath)
    {
        var name = Path.GetFileNameWithoutExtension(vttPath);
        var dot = name.LastIndexOf('.');
        return dot >= 0 && dot < name.Length - 1
            ? name[(dot + 1)..]
            : null;
    }
}
