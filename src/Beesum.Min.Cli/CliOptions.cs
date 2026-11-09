using Beesum.Core.Models;

namespace Beesum.Min.Cli;

/// <summary>Parsed command-line options.</summary>
public sealed record CliOptions(
    string Url,
    PromptStyle Style,
    OutputFormat Format,
    string Provider,
    string? LocalVttPath,
    string? OutputPath,
    string? YtDlpPath,
    string OllamaBaseUrl,
    string OllamaModel,
    bool ShowHelp,
    IReadOnlyList<string>? YtDlpExtraArgs = null,
    bool Json = false,
    bool ShowVersion = false,
    string? ApiUrl = null,
    string? ApiModel = null,
    string? ApiKeyEnv = null,
    int MaxTokens = 1024,
    string? Instructions = null,
    string? PromptFile = null,
    bool Stdin = false,
    string InputFormat = "text",
    string? BatchFile = null,
    bool JsonLines = false,
    bool NoChunk = false,
    int ChunkTokens = 6000,
    IReadOnlyList<string>? SubtitleLanguages = null,
    bool Timestamps = false,
    TimeSpan? From = null,
    TimeSpan? To = null,
    string? SaveVttPath = null,
    string? SaveTranscriptPath = null,
    bool NoSavePrompt = false);
