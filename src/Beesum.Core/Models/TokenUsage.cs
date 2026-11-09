namespace Beesum.Core.Models;

/// <summary>Token counts reported by an LLM provider for one request.</summary>
/// <param name="PromptTokens">Tokens consumed by the prompt (system prompt plus transcript).</param>
/// <param name="CompletionTokens">Tokens produced by the model.</param>
/// <param name="TotalTokens">Total tokens billed or processed.</param>
public sealed record TokenUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens)
{
    /// <summary>Creates usage from prompt and completion counts, deriving the total.</summary>
    /// <param name="promptTokens">Tokens consumed by the prompt.</param>
    /// <param name="completionTokens">Tokens produced by the model.</param>
    public static TokenUsage FromCounts(int promptTokens, int completionTokens) =>
        new(promptTokens, completionTokens, promptTokens + completionTokens);
}
