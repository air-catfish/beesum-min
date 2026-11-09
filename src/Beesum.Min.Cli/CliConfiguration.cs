namespace Beesum.Min.Cli;

/// <summary>Turns CLI options into provider settings. Kept out of <c>Program</c> so it can be tested.</summary>
internal static class CliConfiguration
{
    public static CliConfigurationResult Resolve(
        CliOptions options,
        Func<string, string?>? readEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        readEnvironment ??= Environment.GetEnvironmentVariable;

        var providerId = NormalizeProvider(options.Provider);
        var defaults = GetDefaults(providerId);

        if (defaults is not { } apiDefaults)
        {
            if (options.ApiUrl is not null || options.ApiModel is not null || options.ApiKeyEnv is not null)
            {
                throw new ArgumentException(
                    $"--api-* options need an API provider (openai, anthropic, gemini), not '{options.Provider}'.");
            }

            return new CliConfigurationResult(providerId, null, null, null, options.MaxTokens);
        }

        var baseUrl = options.ApiUrl ?? apiDefaults.BaseUrl;
        var model = options.ApiModel ?? apiDefaults.Model;
        var keyVariable = options.ApiKeyEnv ?? apiDefaults.KeyVariable;
        var apiKey = ReadKey(readEnvironment, keyVariable);

        if (string.IsNullOrWhiteSpace(apiKey) && providerId == "gemini")
            apiKey = readEnvironment("GOOGLE_API_KEY");

        // Local OpenAI-compatible servers (LM Studio, llama.cpp) often have no auth.
        var keyRequired = providerId is "anthropic" or "gemini" || !IsLocal(baseUrl);
        if (keyRequired && string.IsNullOrWhiteSpace(apiKey))
        {
            var fallback = providerId == "gemini" ? " or GOOGLE_API_KEY" : string.Empty;
            throw new ArgumentException(
                $"No API key for {providerId}. Set {keyVariable}{fallback}, or pass --api-key-env <VAR>.");
        }

        return new CliConfigurationResult(providerId, baseUrl, model, apiKey, options.MaxTokens);
    }

    public static string NormalizeProvider(string provider)
    {
        var id = provider.ToLowerInvariant();
        return id switch
        {
            "openai" => "openai-compatible",
            "claude" => "anthropic",
            "google" => "gemini",
            _ => id,
        };
    }

    private static (string BaseUrl, string Model, string KeyVariable)? GetDefaults(string provider) =>
        provider switch
        {
            "openai-compatible" => ("https://api.openai.com", "gpt-4o-mini", "OPENAI_API_KEY"),
            "anthropic" => ("https://api.anthropic.com", "claude-sonnet-4-5", "ANTHROPIC_API_KEY"),
            "gemini" => ("https://generativelanguage.googleapis.com", "gemini-2.5-flash", "GEMINI_API_KEY"),
            _ => null,
        };

    private static string? ReadKey(Func<string, string?> readEnvironment, string? variable)
    {
        if (string.IsNullOrWhiteSpace(variable))
            return null;

        return readEnvironment(variable);
    }

    private static bool IsLocal(string? baseUrl) =>
        Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) &&
        (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
}

internal sealed record CliConfigurationResult(
    string ProviderId,
    string? BaseUrl,
    string? Model,
    string? ApiKey,
    int MaxTokens);

internal static class CliVersion
{
    public static string Value { get; } =
        typeof(CliVersion).Assembly.GetName().Version is { } version
            ? version.ToString(3)
            : "0.0.0";
}
