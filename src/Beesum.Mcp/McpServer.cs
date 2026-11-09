using System.Text.Json;
using Beesum.Core.Abstractions;
using Beesum.Core.Models;

namespace Beesum.Mcp;

/// <summary>
/// Minimal MCP server over stdio: newline-delimited JSON-RPC, tools only. No
/// resources, prompts, or sampling; the surface is intentionally small.
/// </summary>
internal sealed class McpServer
{
    private const string LatestProtocolVersion = "2025-06-18";
    private static readonly string[] SupportedProtocols = { "2024-11-05", "2025-03-26", "2025-06-18" };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ISummarizerPipelineFactory _pipelines;
    private readonly ISubtitleParser _parser;
    private readonly string _defaultProvider;

    public static string Version { get; } =
        typeof(McpServer).Assembly.GetName().Version is { } version
            ? version.ToString(3)
            : "0.0.0";

    public McpServer(
        ISummarizerPipelineFactory pipelines,
        ISubtitleParser parser,
        string defaultProvider = "transcript-only")
    {
        ArgumentNullException.ThrowIfNull(pipelines);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultProvider);

        _pipelines = pipelines;
        _parser = parser;
        _defaultProvider = defaultProvider;
    }

    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null) return;

            // Some Windows pipes start the stream with a UTF-8 BOM.
            line = line.TrimStart('\uFEFF');
            if (string.IsNullOrWhiteSpace(line)) continue;

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                await WriteErrorAsync(output, null, -32700, "Parse error", cancellationToken).ConfigureAwait(false);
                continue;
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    await WriteErrorAsync(output, null, -32600, "Invalid Request", cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var hasId = root.TryGetProperty("id", out var idElement);
                var id = hasId ? idElement.Clone() : (JsonElement?)null;

                var method = root.TryGetProperty("method", out var methodElement) &&
                             methodElement.ValueKind == JsonValueKind.String
                    ? methodElement.GetString()
                    : null;

                if (method is null)
                {
                    if (hasId)
                        await WriteErrorAsync(output, id, -32600, "Invalid Request", cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (method == "exit") return;

                JsonElement? parameters =
                    root.TryGetProperty("params", out var parametersElement) &&
                    parametersElement.ValueKind == JsonValueKind.Object
                        ? parametersElement
                        : null;

                if (method.StartsWith("notifications/", StringComparison.Ordinal))
                    continue;

                if (!hasId)
                    continue;

                switch (method)
                {
                    case "initialize":
                        await WriteResultAsync(output, id, InitializeResult(parameters), cancellationToken).ConfigureAwait(false);
                        break;

                    case "ping":
                        await WriteResultAsync(output, id, new Dictionary<string, object?>(), cancellationToken).ConfigureAwait(false);
                        break;

                    case "shutdown":
                        await WriteResultAsync(output, id, null, cancellationToken).ConfigureAwait(false);
                        break;

                    case "tools/list":
                        await WriteResultAsync(output, id, ToolsList(), cancellationToken).ConfigureAwait(false);
                        break;

                    case "tools/call":
                        await HandleToolCallAsync(output, id, parameters, cancellationToken).ConfigureAwait(false);
                        break;

                    default:
                        await WriteErrorAsync(
                            output,
                            id,
                            -32601,
                            $"Method not found: {method}",
                            cancellationToken).ConfigureAwait(false);
                        break;
                }
            }
        }
    }

    private static Dictionary<string, object?> InitializeResult(JsonElement? parameters)
    {
        var requested = GetString(parameters, "protocolVersion");
        var protocol = requested is not null && SupportedProtocols.Contains(requested, StringComparer.Ordinal)
            ? requested
            : LatestProtocolVersion;

        return new Dictionary<string, object?>
        {
            ["protocolVersion"] = protocol,
            ["capabilities"] = new Dictionary<string, object?>
            {
                ["tools"] = new Dictionary<string, object?> { ["listChanged"] = false },
            },
            ["serverInfo"] = new Dictionary<string, object?>
            {
                ["name"] = "beesum-min",
                ["version"] = Version,
            },
            ["instructions"] =
                "Summarize YouTube videos or supplied transcripts. Prefer summarize_transcript for text you already have, " +
                "summarize_url for YouTube links. Use a prompt for a custom style; the built-in styles are shortcuts.",
        };
    }

    private static Dictionary<string, object?> ToolsList() => new()
    {
        ["tools"] = new object[] { SummarizeUrlTool(), SummarizeTranscriptTool() },
    };

    private static Dictionary<string, object?> SummarizeUrlTool() => new()
    {
        ["name"] = "summarize_url",
        ["description"] = "Download YouTube subtitles with yt-dlp and summarize them.",
        ["inputSchema"] = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>
            {
                ["url"] = new Dictionary<string, object?>
                {
                    ["type"] = "string",
                    ["description"] = "YouTube video URL.",
                },
                ["style"] = StyleSchema(),
                ["format"] = FormatSchema(),
                ["timestamps"] = TimestampsSchema(),
                ["instructions"] = new Dictionary<string, object?>
                {
                    ["type"] = "string",
                    ["description"] = "Extra instructions appended to the built-in prompt.",
                },
                ["prompt"] = new Dictionary<string, object?>
                {
                    ["type"] = "string",
                    ["description"] = "Full system prompt; replaces style and format.",
                },
                ["provider"] = ProviderSchema(),
            },
            ["required"] = new[] { "url" },
            ["additionalProperties"] = false,
        },
    };

    private static Dictionary<string, object?> SummarizeTranscriptTool() => new()
    {
        ["name"] = "summarize_transcript",
        ["description"] = "Summarize a transcript you already have: plain text or WebVTT.",
        ["inputSchema"] = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>
            {
                ["text"] = new Dictionary<string, object?>
                {
                    ["type"] = "string",
                    ["description"] = "Transcript text or WebVTT document.",
                },
                ["input_format"] = new Dictionary<string, object?>
                {
                    ["type"] = "string",
                    ["enum"] = new[] { "text", "vtt" },
                    ["default"] = "text",
                },
                ["style"] = StyleSchema(),
                ["format"] = FormatSchema(),
                ["timestamps"] = TimestampsSchema(),
                ["instructions"] = new Dictionary<string, object?>
                {
                    ["type"] = "string",
                    ["description"] = "Extra instructions appended to the built-in prompt.",
                },
                ["prompt"] = new Dictionary<string, object?>
                {
                    ["type"] = "string",
                    ["description"] = "Full system prompt; replaces style and format.",
                },
                ["provider"] = ProviderSchema(),
            },
            ["required"] = new[] { "text" },
            ["additionalProperties"] = false,
        },
    };

    private static Dictionary<string, object?> StyleSchema() => new()
    {
        ["type"] = "string",
        ["enum"] = new[] { "historian", "programmer", "neutral" },
        ["default"] = "neutral",
        ["description"] = "Built-in prompt shortcut; ignored when prompt is set.",
    };

    private static Dictionary<string, object?> FormatSchema() => new()
    {
        ["type"] = "string",
        ["enum"] = new[] { "markdown", "plaintext", "mindmap" },
        ["default"] = "plaintext",
    };

    private static Dictionary<string, object?> TimestampsSchema() => new()
    {
        ["type"] = "boolean",
        ["default"] = false,
        ["description"] = "Prefix cues with [hh:mm:ss] and ask the model to keep those times.",
    };

    private static Dictionary<string, object?> ProviderSchema() => new()
    {
        ["type"] = "string",
        ["description"] = "Provider id: transcript-only, ollama, openai-compatible, anthropic, gemini.",
    };

    private async Task HandleToolCallAsync(
        TextWriter output,
        JsonElement? id,
        JsonElement? parameters,
        CancellationToken cancellationToken)
    {
        var name = GetString(parameters, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            await WriteErrorAsync(output, id, -32602, "Missing tool name", cancellationToken).ConfigureAwait(false);
            return;
        }

        JsonElement? arguments =
            parameters is { } call &&
            call.TryGetProperty("arguments", out var argsElement) &&
            argsElement.ValueKind == JsonValueKind.Object
                ? argsElement
                : null;

        var outcome = await CallToolAsync(name, arguments, cancellationToken).ConfigureAwait(false);

        var result = new Dictionary<string, object?>
        {
            ["content"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "text",
                    ["text"] = outcome.Text,
                },
            },
        };

        if (outcome.IsError)
            result["isError"] = true;

        await WriteResultAsync(output, id, result, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ToolOutcome> CallToolAsync(
        string name,
        JsonElement? arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            return name switch
            {
                "summarize_url" => await SummarizeUrlAsync(arguments, cancellationToken).ConfigureAwait(false),
                "summarize_transcript" => await SummarizeTranscriptAsync(arguments, cancellationToken).ConfigureAwait(false),
                _ => new ToolOutcome($"Unknown tool: {name}", IsError: true),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            return new ToolOutcome(ex.Message, IsError: true);
        }
        catch (Exception ex)
        {
            return new ToolOutcome($"Tool failed: {ex.Message}", IsError: true);
        }
    }

    private async Task<ToolOutcome> SummarizeUrlAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        var url = GetString(arguments, "url");
        if (string.IsNullOrWhiteSpace(url))
            return new ToolOutcome("Missing required argument: url.", IsError: true);

        if (!IsYouTubeUrl(url))
        {
            return new ToolOutcome(
                "summarize_url only accepts YouTube URLs (youtube.com, youtu.be, music.youtube.com).",
                IsError: true);
        }

        var command = new SummarizeCommand(
            url,
            GetStyle(arguments),
            GetFormat(arguments),
            Instructions: GetString(arguments, "instructions"),
            SystemPrompt: GetString(arguments, "prompt"),
            Timestamps: GetBool(arguments, "timestamps"));

        return await RunPipelineAsync(GetProvider(arguments), command, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ToolOutcome> SummarizeTranscriptAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        var text = GetString(arguments, "text");
        if (string.IsNullOrWhiteSpace(text))
            return new ToolOutcome("Missing required argument: text.", IsError: true);

        var inputFormat = (GetString(arguments, "input_format") ?? "text").ToLowerInvariant();
        var transcript = inputFormat switch
        {
            "text" or "plain" => new Transcript("mcp", string.Empty, new[]
            {
                new SubtitleSegment(TimeSpan.Zero, TimeSpan.Zero, text),
            }),
            "vtt" => _parser.Parse(text, "mcp"),
            _ => throw new ArgumentException("input_format must be text or vtt.", nameof(arguments)),
        };

        var command = new SummarizeCommand(
            Url: string.Empty,
            GetStyle(arguments),
            GetFormat(arguments),
            Instructions: GetString(arguments, "instructions"),
            SystemPrompt: GetString(arguments, "prompt"),
            Transcript: transcript,
            Timestamps: GetBool(arguments, "timestamps"));

        return await RunPipelineAsync(GetProvider(arguments), command, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ToolOutcome> RunPipelineAsync(
        string provider,
        SummarizeCommand command,
        CancellationToken cancellationToken)
    {
        var pipeline = _pipelines.Create(provider);
        var result = await pipeline.RunAsync(command, progress: null, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? new ToolOutcome(result.Content, IsError: false)
            : new ToolOutcome(result.ErrorMessage ?? "Summary failed.", IsError: true);
    }

    private PromptStyle GetStyle(JsonElement? arguments) =>
        (GetString(arguments, "style") ?? "neutral").ToLowerInvariant() switch
        {
            "historian" => PromptStyle.Historian,
            "programmer" => PromptStyle.Programmer,
            "neutral" => PromptStyle.Neutral,
            var other => throw new ArgumentException($"Unknown style: {other}."),
        };

    private static OutputFormat GetFormat(JsonElement? arguments) =>
        (GetString(arguments, "format") ?? "plaintext").ToLowerInvariant() switch
        {
            "markdown" or "md" => OutputFormat.Markdown,
            "plaintext" or "plain" or "text" => OutputFormat.PlainText,
            "mindmap" or "map" or "mermaid" => OutputFormat.MindMap,
            var other => throw new ArgumentException($"Unknown format: {other}."),
        };

    private string GetProvider(JsonElement? arguments) =>
        GetString(arguments, "provider") ?? _defaultProvider;

    private static string? GetString(JsonElement? element, string property) =>
        element is { ValueKind: JsonValueKind.Object } value ? GetString(value, property) : null;

    private static bool GetBool(JsonElement? element, string property) =>
        element is { ValueKind: JsonValueKind.Object } value &&
        value.TryGetProperty(property, out var propertyValue) &&
        propertyValue.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        propertyValue.GetBoolean();

    private static bool IsYouTubeUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is not ("http" or "https")) return false;

        return uri.Host.TrimEnd('.').ToLowerInvariant() is
            "youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com" or
            "youtu.be" or "youtube-nocookie.com" or "www.youtube-nocookie.com";
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static async Task WriteResultAsync(
        TextWriter output,
        JsonElement? id,
        object? result,
        CancellationToken cancellationToken)
    {
        var response = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = result,
        };

        await output.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions)).ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
    }

    private static async Task WriteErrorAsync(
        TextWriter output,
        JsonElement? id,
        int code,
        string message,
        CancellationToken cancellationToken)
    {
        var response = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new Dictionary<string, object?>
            {
                ["code"] = code,
                ["message"] = message,
            },
        };

        await output.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions)).ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
    }

    private sealed record ToolOutcome(string Text, bool IsError);
}
