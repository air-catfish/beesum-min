using Beesum.Core.Models;
using Xunit;

namespace Beesum.Min.Cli.Tests;

/// <summary>
/// Phase 9c-3a: <c>--yt-dlp-args</c> tokenization and parsing.
/// Quotes protect paths with spaces; escape sequences are not interpreted in v0.1.
/// </summary>
public class CliOptionsParserYtDlpArgsTests
{
    private const string Url = "https://www.youtube.com/watch?v=abc123";

    [Fact]
    public void TokenizeArgs_SpaceSeparated_SplitsIntoFlags()
    {
        var tokens = CliOptionsParser.TokenizeArgs("--proxy http://127.0.0.1:1080 --sleep-interval 2");

        Assert.Equal(
            new[] { "--proxy", "http://127.0.0.1:1080", "--sleep-interval", "2" },
            tokens);
    }

    [Fact]
    public void TokenizeArgs_DoubleQuotedValueWithSpaces_KeepsSingleToken()
    {
        var tokens = CliOptionsParser.TokenizeArgs(
            "--cookies \"C:\\path with spaces\\cookies.txt\"");

        Assert.Equal(
            new[] { "--cookies", "C:\\path with spaces\\cookies.txt" },
            tokens);
    }

    [Fact]
    public void TokenizeArgs_SingleQuotedValueWithSpaces_KeepsSingleToken()
    {
        var tokens = CliOptionsParser.TokenizeArgs("--path 'single quoted'");

        Assert.Equal(new[] { "--path", "single quoted" }, tokens);
    }

    [Fact]
    public void TokenizeArgs_EmptyOrWhitespace_ReturnsEmptyList()
    {
        Assert.Empty(CliOptionsParser.TokenizeArgs(string.Empty));
        Assert.Empty(CliOptionsParser.TokenizeArgs("   "));
    }

    [Fact]
    public void TokenizeArgs_UnterminatedQuote_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => CliOptionsParser.TokenizeArgs("\"--unterminated"));

        Assert.Contains("Unterminated quote", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_NetworkFlags_MapToYtDlpArgumentsInOrder()
    {
        var options = CliOptionsParser.Parse(new[]
        {
            Url,
            "--proxy", "socks5://127.0.0.1:1080",
            "--cookies-from-browser", "firefox",
            "--cookies-file", @"C:\cookies.txt",
            "--js-runtime", "deno",
            "--yt-dlp-args", "--sleep-interval 2",
        });

        Assert.NotNull(options.YtDlpExtraArgs);
        Assert.Equal(
            new[]
            {
                "--proxy", "socks5://127.0.0.1:1080",
                "--cookies-from-browser", "firefox",
                "--cookies", @"C:\cookies.txt",
                "--js-runtimes", "deno",
                "--sleep-interval", "2",
            },
            options.YtDlpExtraArgs);
    }

    [Fact]
    public void Parse_JsonFlag_SetsJson()
    {
        Assert.True(CliOptionsParser.Parse(new[] { Url, "--json" }).Json);
        Assert.False(CliOptionsParser.Parse(new[] { Url }).Json);
    }

    [Fact]
    public void HelpText_MentionsNetworkFlagsAndLanguageOverride()
    {
        var help = HelpText.Text;

        Assert.Contains("--proxy", help, StringComparison.Ordinal);
        Assert.Contains("--cookies-file", help, StringComparison.Ordinal);
        Assert.Contains("--js-runtime", help, StringComparison.Ordinal);
        Assert.Contains("BEESUM_LANG", help, StringComparison.Ordinal);
        Assert.Contains("--json", help, StringComparison.Ordinal);
        Assert.Contains("--api-url", help, StringComparison.Ordinal);
        Assert.Contains("--version", help, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WithYtDlpArgsAndOtherOptions_ReturnsAllValues()
    {
        var options = CliOptionsParser.Parse(new[]
        {
            Url,
            "--style", "historian",
            "--format", "markdown",
            "--provider", "ollama",
            "--yt-dlp-args", "--proxy socks5://127.0.0.1:1080 --cookies-from-browser firefox",
        });

        Assert.Equal(Url, options.Url);
        Assert.Equal(PromptStyle.Historian, options.Style);
        Assert.Equal(OutputFormat.Markdown, options.Format);
        Assert.Equal("ollama", options.Provider);
        Assert.NotNull(options.YtDlpExtraArgs);
        Assert.Equal(
            new[] { "--proxy", "socks5://127.0.0.1:1080", "--cookies-from-browser", "firefox" },
            options.YtDlpExtraArgs);
        Assert.False(options.ShowHelp);
    }
    [Fact]
    public void Parse_ApiFlags_MapToOptions()
    {
        var options = CliOptionsParser.Parse(new[]
        {
            Url,
            "--provider", "anthropic",
            "--api-url", "https://proxy.example/anthropic",
            "--api-model", "claude-sonnet-4-5",
            "--api-key-env", "MY_ANTHROPIC_KEY",
            "--max-tokens", "2048",
        });

        Assert.Equal("anthropic", options.Provider);
        Assert.Equal("https://proxy.example/anthropic", options.ApiUrl);
        Assert.Equal("claude-sonnet-4-5", options.ApiModel);
        Assert.Equal("MY_ANTHROPIC_KEY", options.ApiKeyEnv);
        Assert.Equal(2048, options.MaxTokens);
    }

    [Fact]
    public void Parse_VersionAndJson_AreIndependentFlags()
    {
        var version = CliOptionsParser.Parse(new[] { "--version" });
        var json = CliOptionsParser.Parse(new[] { Url, "--json" });

        Assert.True(version.ShowVersion);
        Assert.False(version.Json);
        Assert.True(json.Json);
        Assert.False(json.ShowVersion);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void Parse_MaxTokensRejectsNonPositiveValues(string value)
    {
        Assert.Throws<ArgumentException>(
            () => CliOptionsParser.Parse(new[] { Url, "--max-tokens", value }));
    }
    [Fact]
    public void Parse_PromptFlags_MapToOptions()
    {
        var options = CliOptionsParser.Parse(new[]
        {
            Url,
            "--instructions", "five bullets",
            "--prompt-file", "prompts/ru.md",
        });

        Assert.Equal("five bullets", options.Instructions);
        Assert.Equal("prompts/ru.md", options.PromptFile);
    }    [Fact]
    public void Parse_StdinFlags_MapToOptions()
    {
        var options = CliOptionsParser.Parse(new[] { "--stdin", "--input", "vtt" });

        Assert.True(options.Stdin);
        Assert.Equal("vtt", options.InputFormat);
        Assert.Equal(string.Empty, options.Url);
    }

    [Fact]
    public void Parse_StdinWithUrl_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => CliOptionsParser.Parse(new[] { Url, "--stdin" }));
    }

    [Fact]
    public void Parse_InputWithoutStdin_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => CliOptionsParser.Parse(new[] { Url, "--input", "text" }));
    }    [Fact]
    public void Parse_BatchFlags_MapToOptions()
    {
        var options = CliOptionsParser.Parse(new[] { "--batch", "urls.txt", "--jsonl" });

        Assert.Equal("urls.txt", options.BatchFile);
        Assert.True(options.JsonLines);
        Assert.True(options.Json);
    }

    [Fact]
    public void Parse_BatchWithUrl_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => CliOptionsParser.Parse(new[] { Url, "--batch", "urls.txt" }));
    }

    [Fact]
    public void Parse_BatchWithStdin_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => CliOptionsParser.Parse(new[] { "--batch", "urls.txt", "--stdin" }));
    }    [Fact]
    public void Parse_JsonLinesWithoutBatch_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => CliOptionsParser.Parse(new[] { Url, "--jsonl" }));
    }    [Fact]
    public void Parse_ChunkFlags_MapToOptions()
    {
        var options = CliOptionsParser.Parse(new[] { Url, "--no-chunk", "--chunk-tokens", "2048" });

        Assert.True(options.NoChunk);
        Assert.Equal(2048, options.ChunkTokens);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("nope")]
    public void Parse_ChunkTokensRejectsInvalidValues(string value)
    {
        Assert.Throws<ArgumentException>(
            () => CliOptionsParser.Parse(new[] { Url, "--chunk-tokens", value }));
    }    [Fact]
    public void Parse_SubtitleLanguages_SplitOnCommas()
    {
        var options = CliOptionsParser.Parse(new[] { Url, "--sub-langs", "ru, en" });

        Assert.Equal(new[] { "ru", "en" }, options.SubtitleLanguages);
    }

    [Fact]
    public void Parse_SubtitleLanguages_RejectsEmptyList()
    {
        Assert.Throws<ArgumentException>(
            () => CliOptionsParser.Parse(new[] { Url, "--sub-langs", ", ," }));
    }}
