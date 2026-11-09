using Beesum.Core.Models;

namespace Beesum.Core.Llm;

/// <summary>Shared system-prompt builder for LLM providers.</summary>
public static class LlmPrompts
{
    /// <summary>Builds the system prompt for a style and output format.</summary>
    /// <param name="style">Requested summary style.</param>
    /// <param name="format">Requested output format.</param>
    public static string BuildSystemPrompt(PromptStyle style, OutputFormat format) =>
        BuildSystemPrompt(style, format, instructions: null);

    /// <summary>Builds the system prompt and appends optional extra instructions.</summary>
    /// <param name="style">Requested summary style.</param>
    /// <param name="format">Requested output format.</param>
    /// <param name="instructions">Extra instructions; blank values are ignored.</param>
    /// <param name="timestamps">Whether to ask for <c>[hh:mm:ss]</c> cue times.</param>
    public static string BuildSystemPrompt(
        PromptStyle style,
        OutputFormat format,
        string? instructions,
        bool timestamps = false)
    {
        var prompt = BuildBasePrompt(style, format, timestamps);
        return string.IsNullOrWhiteSpace(instructions)
            ? prompt
            : prompt + "\n\n" + instructions.Trim();
    }

    /// <summary>
    /// Builds the system prompt for a request: <see cref="SummaryRequest.SystemPrompt"/>
    /// wins, otherwise style/format plus <see cref="SummaryRequest.Instructions"/>.
    /// </summary>
    /// <param name="request">Request whose prompt is built.</param>
    public static string BuildSystemPrompt(SummaryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return string.IsNullOrWhiteSpace(request.SystemPrompt)
            ? BuildSystemPrompt(request.Style, request.Format, request.Instructions, request.Timestamps)
            : request.SystemPrompt.Trim();
    }

    private static string BuildBasePrompt(PromptStyle style, OutputFormat format, bool timestamps)
    {
        var styleText = style switch
        {
            PromptStyle.Historian => "You are a historian. Write in a narrative, contextual tone.",
            PromptStyle.Programmer => "You are a software engineer. Focus on technical details, code, and precise terminology.",
            PromptStyle.Neutral => "You are a neutral assistant. Be objective and concise.",
            _ => "You are a neutral assistant.",
        };

        var formatText = format switch
        {
            OutputFormat.Markdown =>
                "Format the answer as Markdown with headings and bullet lists.",
            OutputFormat.MindMap =>
                "Return a Markdown nested bullet list for a mindmap. The first bullet is the central topic; " +
                "indent children by two spaces for two levels. Use short labels, no fences, no prose.",
            _ => "Return plain text only, without Markdown markup.",
        };

        var timestampText = timestamps
            ? " Keep timestamps in [hh:mm:ss] form when citing moments."
            : string.Empty;

        return $"{styleText} {formatText}{timestampText} Summarize the following transcript.";
    }
}
