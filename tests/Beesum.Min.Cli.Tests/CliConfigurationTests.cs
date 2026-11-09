using Beesum.Core.Models;
using Beesum.Min.Cli;
using Xunit;

namespace Beesum.Min.Cli.Tests;

public class CliConfigurationTests
{
    private static CliOptions Options(
        string provider,
        string? apiUrl = null,
        string? apiModel = null,
        string? apiKeyEnv = null,
        int maxTokens = 1024) =>
        new(
            string.Empty,
            PromptStyle.Neutral,
            OutputFormat.PlainText,
            provider,
            null,
            null,
            null,
            "http://localhost:11434",
            "llama3.2",
            false,
            null,
            false,
            false,
            apiUrl,
            apiModel,
            apiKeyEnv,
            maxTokens);

    private static Func<string, string?> Env(params (string Name, string Value)[] entries) =>
        name => Array.Find(entries, entry => entry.Name == name).Value;

    [Fact]
    public void Resolve_OpenAi_UsesDefaultsAndEnvironmentKey()
    {
        var config = CliConfiguration.Resolve(
            Options("openai"),
            Env(("OPENAI_API_KEY", "sk-test")));

        Assert.Equal("openai-compatible", config.ProviderId);
        Assert.Equal("https://api.openai.com", config.BaseUrl);
        Assert.Equal("gpt-4o-mini", config.Model);
        Assert.Equal("sk-test", config.ApiKey);
    }

    [Fact]
    public void Resolve_ClaudeAlias_UsesAnthropicDefaults()
    {
        var config = CliConfiguration.Resolve(
            Options("claude"),
            Env(("ANTHROPIC_API_KEY", "sk-ant")));

        Assert.Equal("anthropic", config.ProviderId);
        Assert.Equal("https://api.anthropic.com", config.BaseUrl);
        Assert.Equal("claude-sonnet-4-5", config.Model);
    }

    [Fact]
    public void Resolve_Gemini_FallsBackToGoogleApiKey()
    {
        var config = CliConfiguration.Resolve(
            Options("gemini"),
            Env(("GOOGLE_API_KEY", "AIza")));

        Assert.Equal("gemini", config.ProviderId);
        Assert.Equal("AIza", config.ApiKey);
    }

    [Fact]
    public void Resolve_AnthropicWithoutKey_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => CliConfiguration.Resolve(Options("anthropic"), Env()));

        Assert.Contains("ANTHROPIC_API_KEY", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_LocalOpenAiWithoutKey_IsAllowed()
    {
        var config = CliConfiguration.Resolve(
            Options("openai", apiUrl: "http://localhost:1234"),
            Env());

        Assert.Equal("http://localhost:1234", config.BaseUrl);
        Assert.Null(config.ApiKey);
    }

    [Fact]
    public void Resolve_ApiFlagsWithOllama_Throw()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => CliConfiguration.Resolve(Options("ollama", apiUrl: "https://api.openai.com"), Env()));

        Assert.Contains("api-*", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_ExplicitUrlModelAndKeyVariable_OverrideDefaults()
    {
        var config = CliConfiguration.Resolve(
            Options(
                "openai",
                apiUrl: "https://openrouter.ai/api",
                apiModel: "anthropic/claude-sonnet-4-5",
                apiKeyEnv: "OPENROUTER_API_KEY",
                maxTokens: 2048),
            Env(("OPENROUTER_API_KEY", "or-key")));

        Assert.Equal("https://openrouter.ai/api", config.BaseUrl);
        Assert.Equal("anthropic/claude-sonnet-4-5", config.Model);
        Assert.Equal("or-key", config.ApiKey);
        Assert.Equal(2048, config.MaxTokens);
    }

    [Fact]
    public void Resolve_Ollama_PassesThrough()
    {
        var config = CliConfiguration.Resolve(Options("ollama"), Env());

        Assert.Equal("ollama", config.ProviderId);
        Assert.Null(config.BaseUrl);
        Assert.Null(config.ApiKey);
    }

    [Fact]
    public void Version_IsSemantic()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", CliVersion.Value);
    }
}
