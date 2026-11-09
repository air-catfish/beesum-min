namespace Beesum.Core.Models;

/// <summary>Result of probing an LLM provider for availability.</summary>
/// <param name="IsAvailable">Whether the provider can serve requests right now.</param>
/// <param name="StatusMessage">Optional diagnostic text (e.g. <c>"model not pulled"</c>).</param>
/// <param name="ModelName">Model the provider will use, if known.</param>
public sealed record LlmAvailability(
    bool IsAvailable,
    string? StatusMessage = null,
    string? ModelName = null);
