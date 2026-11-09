using System.Diagnostics.CodeAnalysis;

namespace Beesum.Core.Models;

/// <summary>Command that drives the summarization pipeline.</summary>
/// <param name="Url">YouTube URL to summarize.</param>
/// <param name="Style">Summary style.</param>
/// <param name="Format">Output format.</param>
/// <param name="LocalVttPath">Optional local <c>.vtt</c> file used instead of downloading.</param>
/// <param name="Instructions">Extra prompt instructions.</param>
/// <param name="SystemPrompt">Full system-prompt override.</param>
/// <param name="Transcript">Pre-parsed transcript; when set, the pipeline skips download and parsing.</param>
/// <param name="Timestamps">Whether output and prompts should use <c>[hh:mm:ss]</c> cue times.</param>
/// <param name="From">Optional clip start.</param>
/// <param name="To">Optional clip end.</param>
/// <param name="SaveVttPath">Optional path where the full downloaded/local VTT is copied.</param>
/// <param name="SaveTranscriptPath">Optional path where the clipped transcript is written.</param>
/// <param name="NoChunk">Disable map-reduce chunking for this command.</param>
public sealed record SummarizeCommand(
    [property: SuppressMessage(
        "Design",
        "CA1056:URI properties should not be strings",
        Justification = "UI accepts URLs as plain strings; validated at the pipeline boundary.")]
    string Url,
    PromptStyle Style,
    OutputFormat Format,
    string? LocalVttPath = null,
    string? Instructions = null,
    string? SystemPrompt = null,
    Transcript? Transcript = null,
    bool Timestamps = false,
    TimeSpan? From = null,
    TimeSpan? To = null,
    string? SaveVttPath = null,
    string? SaveTranscriptPath = null,
    bool NoChunk = false);
