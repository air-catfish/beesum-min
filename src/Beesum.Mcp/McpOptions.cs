using Beesum.Core;

namespace Beesum.Mcp;

/// <summary>Environment-driven configuration for the MCP server.</summary>
internal static class McpOptions
{
    public static string DefaultProvider =>
        Environment.GetEnvironmentVariable("BEESUM_PROVIDER") is { Length: > 0 } value
            ? value
            : "transcript-only";

    public static void Configure(BeesumOptions options)
    {
        options.LlmProviderId = DefaultProvider;

        options.OllamaBaseUrl = Env("BEESUM_OLLAMA_URL") ?? "http://localhost:11434";
        options.OllamaModel = Env("BEESUM_OLLAMA_MODEL") ?? "llama3.2";

        var openAiKey = Env("OPENAI_API_KEY");
        if (openAiKey is not null)
        {
            options.OpenAiBaseUrl = Env("BEESUM_OPENAI_URL") ?? "https://api.openai.com";
            options.OpenAiModel = Env("BEESUM_OPENAI_MODEL") ?? "gpt-4o-mini";
            options.OpenAiApiKeyProvider = _ => Task.FromResult<string?>(openAiKey);
        }

        var anthropicKey = Env("ANTHROPIC_API_KEY");
        if (anthropicKey is not null)
        {
            options.AnthropicBaseUrl = Env("BEESUM_ANTHROPIC_URL") ?? "https://api.anthropic.com";
            options.AnthropicModel = Env("BEESUM_ANTHROPIC_MODEL") ?? "claude-sonnet-4-5";
            options.AnthropicApiKeyProvider = _ => Task.FromResult<string?>(anthropicKey);
        }

        var geminiKey = Env("GEMINI_API_KEY") ?? Env("GOOGLE_API_KEY");
        if (geminiKey is not null)
        {
            options.GeminiBaseUrl = Env("BEESUM_GEMINI_URL") ?? "https://generativelanguage.googleapis.com";
            options.GeminiModel = Env("BEESUM_GEMINI_MODEL") ?? "gemini-2.5-flash";
            options.GeminiApiKeyProvider = _ => Task.FromResult<string?>(geminiKey);
        }

        var proxy = Env("BEESUM_PROXY");
        if (proxy is not null)
        {
            options.YtDlpExtraArgs.Add("--proxy");
            options.YtDlpExtraArgs.Add(proxy);
        }

        var languages = Env("BEESUM_SUB_LANGS");
        if (languages is not null)
        {
            options.SubtitleLanguages.Clear();
            foreach (var language in languages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                options.SubtitleLanguages.Add(language);
        }
    }

    private static string? Env(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;
}
