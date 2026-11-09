using System.Globalization;
using System.Text;
using Beesum.Core.Models;

namespace Beesum.Min.Cli;

public static class CliOptionsParser
{
    public static CliOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var url = string.Empty;
        var style = PromptStyle.Neutral;
        var format = OutputFormat.PlainText;
        var provider = "transcript-only";
        string? localVtt = null;
        string? output = null;
        string? ytDlp = null;
        var ollamaUrl = "http://localhost:11434";
        var ollamaModel = "llama3.2";
        var showHelp = false;
        var showVersion = false;
        var json = false;
        string? apiUrl = null;
        string? apiModel = null;
        string? apiKeyEnv = null;
        var maxTokens = 1024;
        string? instructions = null;
        string? promptFile = null;
        var stdin = false;
        var inputFormat = "text";
        var inputSeen = false;
        string? batchFile = null;
        var jsonLines = false;
        var noChunk = false;
        var chunkTokens = 6000;
        IReadOnlyList<string>? subtitleLanguages = null;
        var timestamps = false;
        var ytDlpArgs = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-h":
                case "--help":
                    showHelp = true;
                    break;

                case "--style":
                    style = ParseStyle(RequireValue(args, ref i));
                    break;

                case "--format":
                    format = ParseFormat(RequireValue(args, ref i));
                    break;

                case "--provider":
                    provider = RequireValue(args, ref i);
                    break;

                case "--local-vtt":
                    localVtt = RequireValue(args, ref i);
                    break;

                case "--output":
                case "-o":
                    output = RequireValue(args, ref i);
                    break;

                case "--yt-dlp":
                    ytDlp = RequireValue(args, ref i);
                    break;

                case "--proxy":
                    ytDlpArgs.Add("--proxy");
                    ytDlpArgs.Add(RequireValue(args, ref i));
                    break;

                case "--cookies-from-browser":
                    ytDlpArgs.Add("--cookies-from-browser");
                    ytDlpArgs.Add(RequireValue(args, ref i));
                    break;

                case "--cookies-file":
                    ytDlpArgs.Add("--cookies");
                    ytDlpArgs.Add(RequireValue(args, ref i));
                    break;

                case "--js-runtime":
                    ytDlpArgs.Add("--js-runtimes");
                    ytDlpArgs.Add(RequireValue(args, ref i));
                    break;

                case "--yt-dlp-args":
                    ytDlpArgs.AddRange(TokenizeArgs(RequireValue(args, ref i)));
                    break;

                case "--json":
                    json = true;
                    break;

                case "--version":
                    showVersion = true;
                    break;

                case "--instructions":
                    instructions = RequireValue(args, ref i);
                    break;

                case "--prompt-file":
                    promptFile = RequireValue(args, ref i);
                    break;

                case "--api-url":
                    apiUrl = RequireValue(args, ref i);
                    break;

                case "--api-model":
                    apiModel = RequireValue(args, ref i);
                    break;

                case "--api-key-env":
                    apiKeyEnv = RequireValue(args, ref i);
                    break;

                case "--sub-langs":
                    subtitleLanguages = ParseLanguages(RequireValue(args, ref i));
                    break;

                case "--timestamps":
                    timestamps = true;
                    break;

                case "--no-chunk":
                    noChunk = true;
                    break;

                case "--chunk-tokens":
                    chunkTokens = ParsePositiveInt(RequireValue(args, ref i), "--chunk-tokens");
                    break;

                case "--batch":
                    batchFile = RequireValue(args, ref i);
                    break;

                case "--jsonl":
                    jsonLines = true;
                    json = true;
                    break;

                case "--stdin":
                    stdin = true;
                    break;

                case "--input":
                    inputFormat = ParseInputFormat(RequireValue(args, ref i));
                    inputSeen = true;
                    break;

                case "--max-tokens":
                    maxTokens = ParseMaxTokens(RequireValue(args, ref i));
                    break;

                case "--ollama-url":
                    ollamaUrl = RequireValue(args, ref i);
                    break;

                case "--ollama-model":
                    ollamaModel = RequireValue(args, ref i);
                    break;

                default:
                    if (arg.StartsWith('-'))
                        throw new ArgumentException($"Unknown option '{arg}'.");
                    if (!string.IsNullOrEmpty(url))
                        throw new ArgumentException($"Unexpected extra positional argument '{arg}'.");
                    url = arg;
                    break;
            }
        }

        if (jsonLines && batchFile is null)
            throw new ArgumentException("--jsonl requires --batch.");

        if (batchFile is not null)
        {
            if (!string.IsNullOrWhiteSpace(localVtt))
                throw new ArgumentException("--batch and --local-vtt are mutually exclusive.");
            if (stdin)
                throw new ArgumentException("--batch and --stdin are mutually exclusive.");
            if (!string.IsNullOrEmpty(url))
                throw new ArgumentException("--batch takes URLs from a file; do not pass one on the command line.");
        }

        if (inputSeen && !stdin)
            throw new ArgumentException("--input requires --stdin.");
        if (stdin && !string.IsNullOrEmpty(url))
            throw new ArgumentException("URL and --stdin are mutually exclusive.");
        if (stdin && !string.IsNullOrWhiteSpace(localVtt))
            throw new ArgumentException("--stdin and --local-vtt are mutually exclusive.");

        if (!showHelp && !showVersion && !stdin && batchFile is null && string.IsNullOrEmpty(url) && string.IsNullOrWhiteSpace(localVtt))
            throw new ArgumentException("URL is required (or pass --local-vtt <path>, or --stdin).");

        return new CliOptions(
            url,
            style,
            format,
            provider,
            localVtt,
            output,
            ytDlp,
            ollamaUrl,
            ollamaModel,
            showHelp,
            ytDlpArgs.Count == 0 ? null : ytDlpArgs,
            json,
            showVersion,
            apiUrl,
            apiModel,
            apiKeyEnv,
            maxTokens,
            instructions,
            promptFile,
            stdin,
            inputFormat,
            batchFile,
            jsonLines,
            noChunk,
            chunkTokens,
            subtitleLanguages,
            timestamps);
    }

    /// <summary>
    /// Splits <paramref name="input"/> into yt-dlp arguments, honoring single and double
    /// quotes so paths with spaces survive. Escape sequences are not interpreted in v0.1.
    /// </summary>
    /// <exception cref="ArgumentException">The input contains an unterminated quote.</exception>
    internal static IReadOnlyList<string> TokenizeArgs(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Array.Empty<string>();

        var tokens = new List<string>();
        var current = new StringBuilder();
        var quote = '\0';
        var hasToken = false;

        foreach (var c in input)
        {
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"' || c == '\'')
            {
                quote = c;
                hasToken = true;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (hasToken)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }
            }
            else
            {
                current.Append(c);
                hasToken = true;
            }
        }

        if (quote != '\0')
            throw new ArgumentException("Unterminated quote in --yt-dlp-args.");

        if (hasToken)
            tokens.Add(current.ToString());

        return tokens;
    }

    private static string ParseInputFormat(string value) => value.ToLowerInvariant() switch
    {
        "text" or "txt" => "text",
        "vtt" => "vtt",
        _ => throw new ArgumentException($"Unknown --input '{value}'. Use text|vtt."),
    };

    private static IReadOnlyList<string> ParseLanguages(string value)
    {
        var languages = value.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (languages.Length == 0)
            throw new ArgumentException("--sub-langs needs at least one language.");

        return languages;
    }

    private static int ParsePositiveInt(string value, string option)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) || result <= 0)
            throw new ArgumentException($"{option} must be a positive integer, got '{value}'.");

        return result;
    }

    private static int ParseMaxTokens(string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var tokens) || tokens <= 0)
            throw new ArgumentException($"--max-tokens must be a positive integer, got '{value}'.");

        return tokens;
    }

    private static string RequireValue(string[] args, ref int i)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException($"Option '{args[i]}' requires a value.");
        i++;
        return args[i];
    }

    private static PromptStyle ParseStyle(string value) =>
        value.ToLowerInvariant() switch
        {
            "historian" => PromptStyle.Historian,
            "programmer" => PromptStyle.Programmer,
            "neutral" => PromptStyle.Neutral,
            _ => throw new ArgumentException(
                $"Unknown --style '{value}'. Use historian|programmer|neutral."),
        };

    private static OutputFormat ParseFormat(string value) =>
        value.ToLowerInvariant() switch
        {
            "markdown" or "md" => OutputFormat.Markdown,
            "plaintext" or "plain" or "txt" => OutputFormat.PlainText,
            "mindmap" or "map" or "mermaid" => OutputFormat.MindMap,
            _ => throw new ArgumentException(
                $"Unknown --format '{value}'. Use markdown|plaintext|mindmap."),
        };
}
