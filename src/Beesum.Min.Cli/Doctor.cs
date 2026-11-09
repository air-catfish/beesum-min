
using System.Text.Json;

using Beesum.Core.Abstractions;
using Beesum.Core.YtDlp;
using Microsoft.Extensions.DependencyInjection;


namespace Beesum.Min.Cli;

internal static class Doctor
{
    private const string UserAgent = "beesum-min-doctor";

    public static async Task<int> RunAsync(
        CliOptions options,
        IServiceProvider services,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(output);

        var checks = new List<DoctorCheck>
        {
            new("beesum-min", DoctorStatus.Ok, CliVersion.Value),
        };

        var runner = services.GetRequiredService<IProcessRunner>();
        var resolver = services.GetRequiredService<IYtDlpPathResolver>();

        YtDlpResolution? resolution = null;
        try
        {
            resolution = await resolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            checks.Add(new("yt-dlp", DoctorStatus.Fail, ex.Message));
        }

        string? ytDlpVersion = null;
        if (resolution is null)
        {
            checks.Add(new("yt-dlp", DoctorStatus.Fail, "not found; install it or pass --yt-dlp"));
        }
        else
        {
            ytDlpVersion = await TryVersionAsync(runner, resolution.Path, cancellationToken).ConfigureAwait(false);
            checks.Add(ytDlpVersion is null
                ? new("yt-dlp", DoctorStatus.Fail, $"could not run {resolution.Path}")
                : new("yt-dlp", DoctorStatus.Ok, $"{ytDlpVersion} ({resolution.Source}, {resolution.Path})"));
        }

        var deno = await TryVersionAsync(runner, "deno", cancellationToken).ConfigureAwait(false);
        checks.Add(deno is null
            ? new("deno", DoctorStatus.Warn, "not found; YouTube challenges may fail")
            : new("deno", DoctorStatus.Ok, deno));

        checks.Add(TempCheck());

        var ollama = await CheckOllamaAsync(services, options, cancellationToken).ConfigureAwait(false);
        checks.Add(ollama);

        var provider = CliConfiguration.NormalizeProvider(options.Provider);
        checks.Add(ProviderKeyCheck(provider));

        if (options.CheckUpdates)
        {
            checks.AddRange(await CheckUpdatesAsync(ytDlpVersion, cancellationToken).ConfigureAwait(false));
        }

        var healthy = checks.All(check => check.Status != DoctorStatus.Fail);

        if (options.Json)
        {
            var json = JsonSerializer.Serialize(new
            {
                isHealthy = healthy,
                version = CliVersion.Value,
                checks = checks.Select(check => new
                {
                    name = check.Name,
                    status = check.Status.ToString().ToLowerInvariant(),
                    detail = check.Detail,
                }),
            }, new JsonSerializerOptions { WriteIndented = true });

            await output.WriteLineAsync(json).ConfigureAwait(false);
        }
        else
        {
            await output.WriteLineAsync($"beesum-min doctor {CliVersion.Value}").ConfigureAwait(false);
            foreach (var check in checks)
            {
                var marker = check.Status switch
                {
                    DoctorStatus.Ok => "ok  ",
                    DoctorStatus.Warn => "warn",
                    _ => "fail",
                };
                await output.WriteLineAsync($"[{marker}] {check.Name}: {check.Detail}").ConfigureAwait(false);
            }

            await output.WriteLineAsync(healthy
                ? "Environment looks usable."
                : "At least one required check failed.").ConfigureAwait(false);
        }

        return healthy ? 0 : 1;
    }

    private static async Task<string?> TryVersionAsync(
        IProcessRunner runner,
        string fileName,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            var result = await runner.RunAsync(
                fileName,
                new[] { "--version" },
                cancellationToken: timeout.Token).ConfigureAwait(false);

            if (result.ExitCode != 0) return null;

            return result.StandardOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static DoctorCheck TempCheck()
    {
        var path = Path.Combine(Path.GetTempPath(), "beesum-doctor-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(path, "ok");
            File.Delete(path);
            return new DoctorCheck("temp", DoctorStatus.Ok, Path.GetTempPath());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new DoctorCheck("temp", DoctorStatus.Fail, $"not writable: {ex.Message}");
        }
    }

    private static async Task<DoctorCheck> CheckOllamaAsync(
        IServiceProvider services,
        CliOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.OllamaBaseUrl))
            return new DoctorCheck("ollama", DoctorStatus.Warn, "disabled");

        var factory = services.GetService<IHttpClientFactory>();
        if (factory is null)
            return new DoctorCheck("ollama", DoctorStatus.Warn, "no HTTP client registered");

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));

            var client = factory.CreateClient("ollama");
            var baseUri = options.OllamaBaseUrl.TrimEnd('/') + "/";
            var response = await client.GetAsync(new Uri(new Uri(baseUri), "api/version"), timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new DoctorCheck("ollama", DoctorStatus.Warn, $"HTTP {(int)response.StatusCode}");

            var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            using var json = JsonDocument.Parse(body);
            var version = json.RootElement.TryGetProperty("version", out var value) ? value.GetString() : null;
            return new DoctorCheck("ollama", DoctorStatus.Ok, version ?? "reachable");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new DoctorCheck("ollama", DoctorStatus.Warn, "timed out");
        }
        catch (Exception ex)
        {
            return new DoctorCheck("ollama", DoctorStatus.Warn, ex.Message);
        }
    }

    private static DoctorCheck ProviderKeyCheck(string provider)
    {
        var variable = provider switch
        {
            "openai-compatible" => "OPENAI_API_KEY",
            "anthropic" => "ANTHROPIC_API_KEY",
            "gemini" => "GEMINI_API_KEY",
            _ => null,
        };

        if (variable is null)
            return new DoctorCheck("provider", DoctorStatus.Ok, $"{provider} needs no key");

        return string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable))
            ? new DoctorCheck("provider", DoctorStatus.Warn, $"{provider}: {variable} is not set")
            : new DoctorCheck("provider", DoctorStatus.Ok, $"{provider}: {variable} is set");
    }

    private static async Task<IReadOnlyList<DoctorCheck>> CheckUpdatesAsync(
        string? ytDlpVersion,
        CancellationToken cancellationToken)
    {
        var checks = new List<DoctorCheck>();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

        try
        {
            var latest = await LatestNuGetVersionAsync(http, cancellationToken).ConfigureAwait(false);
            if (latest is null)
            {
                checks.Add(new DoctorCheck("updates", DoctorStatus.Warn, "could not read NuGet index"));
            }
            else if (IsNewer(latest, CliVersion.Value))
            {
                checks.Add(new DoctorCheck(
                    "updates",
                    DoctorStatus.Warn,
                    $"beesum-min {CliVersion.Value} -> {latest}; run: dotnet tool update -g beesum-min"));
            }
            else
            {
                checks.Add(new DoctorCheck("updates", DoctorStatus.Ok, $"beesum-min {CliVersion.Value} is current"));
            }
        }
        catch (Exception ex)
        {
            checks.Add(new DoctorCheck("updates", DoctorStatus.Warn, ex.Message));
        }

        if (ytDlpVersion is not null)
        {
            try
            {
                var latest = await LatestYtDlpVersionAsync(http, cancellationToken).ConfigureAwait(false);
                if (latest is not null && IsNewer(latest, ytDlpVersion))
                {
                    checks.Add(new DoctorCheck(
                        "updates",
                        DoctorStatus.Warn,
                        $"yt-dlp {ytDlpVersion} -> {latest}"));
                }
            }
            catch (Exception ex)
            {
                checks.Add(new DoctorCheck("updates", DoctorStatus.Warn, ex.Message));
            }
        }

        return checks;
    }

    private static async Task<string?> LatestNuGetVersionAsync(HttpClient http, CancellationToken cancellationToken)
    {
        var body = await http.GetStringAsync(
            "https://api.nuget.org/v3-flatcontainer/beesum-min/index.json",
            cancellationToken).ConfigureAwait(false);
        using var json = JsonDocument.Parse(body);
        if (!json.RootElement.TryGetProperty("versions", out var versions)) return null;

        return versions.EnumerateArray()
            .Select(value => value.GetString())
            .Where(value => value is not null && !value.Contains('-'))
            .LastOrDefault();
    }

    private static async Task<string?> LatestYtDlpVersionAsync(HttpClient http, CancellationToken cancellationToken)
    {
        var body = await http.GetStringAsync(
            "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest",
            cancellationToken).ConfigureAwait(false);
        using var json = JsonDocument.Parse(body);
        return json.RootElement.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null;
    }

    private static bool IsNewer(string candidate, string current)
    {
        return Version.TryParse(candidate.TrimStart('v'), out var left) &&
               Version.TryParse(current.TrimStart('v'), out var right) &&
               left > right;
    }

    internal sealed record DoctorCheck(string Name, DoctorStatus Status, string Detail);

    internal enum DoctorStatus
    {
        Ok,
        Warn,
        Fail,
    }
}
