namespace Beesum.Core.Models;

/// <summary>Input to <see cref="Abstractions.ILlmProvider.SummarizeAsync"/>.</summary>
/// <param name="Transcript">Transcript to summarize.</param>
/// <param name="Style">Requested summary style.</param>
/// <param name="Format">Requested output format.</param>
/// <param name="Instructions">Extra instructions appended to the built system prompt, if any.</param>
/// <param name="SystemPrompt">Full system-prompt override; when set, it replaces style and format.</param>
/// <param name="Timestamps">Whether to include <c>[hh:mm:ss]</c> cue times and ask the model to keep them.</param>
public sealed record SummaryRequest(
    Transcript Transcript,
    PromptStyle Style,
    OutputFormat Format,
    string? Instructions = null,
    string? SystemPrompt = null,
    bool Timestamps = false);
