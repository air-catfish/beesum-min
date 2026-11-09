namespace Beesum.Core.Models;

/// <summary>A single entry in the summary history.</summary>
/// <param name="Id">Stable entry id (GUID or timestamp-based).</param>
/// <param name="SourceId">Provider source id (e.g. YouTube video id).</param>
/// <param name="Title">Human-readable title captured at creation time.</param>
/// <param name="CreatedAt">When the summary was produced.</param>
/// <param name="Result">The summary itself.</param>
public sealed record SummaryHistoryEntry(
    string Id,
    string SourceId,
    string Title,
    DateTimeOffset CreatedAt,
    SummaryResult Result);
