namespace Beesum.Core.Models;

/// <summary>
/// Result of a summary operation. Expected failures are represented by
/// <see cref="IsSuccess"/> = <c>false</c> + <see cref="ErrorMessage"/>; exceptions
/// are reserved for programming errors.
/// </summary>
/// <param name="IsSuccess">Whether the summary was produced.</param>
/// <param name="Content">Summary text, or an empty string on failure.</param>
/// <param name="ProviderId">Id of the producing provider, or <c>null</c> if the pipeline failed before invoking one.</param>
/// <param name="ErrorMessage">Human-readable failure reason, or <c>null</c> on success.</param>
/// <param name="ModelName">Model reported by the provider, when known.</param>
/// <param name="Usage">Token usage reported by the provider, when available.</param>
/// <param name="Duration">Time spent in the provider call, when measured.</param>
public sealed record SummaryResult(
    bool IsSuccess,
    string Content,
    string? ProviderId = null,
    string? ErrorMessage = null,
    string? ModelName = null,
    TokenUsage? Usage = null,
    TimeSpan? Duration = null);
