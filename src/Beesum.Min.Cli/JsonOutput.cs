using System.Text.Json;
using Beesum.Core.Models;

namespace Beesum.Min.Cli;

/// <summary>Serializes a <see cref="SummaryResult"/> into the stable CLI JSON shape.</summary>
internal static class JsonOutput
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions CompactOptions = new(Options)
    {
        WriteIndented = false,
    };

    public static string Serialize(SummaryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return JsonSerializer.Serialize(ToPayload(result), Options);
    }

    /// <summary>Batch output: a JSON array, or one compact object per line.</summary>
    public static string SerializeBatch(IReadOnlyList<BatchItem> items, bool lines)
    {
        ArgumentNullException.ThrowIfNull(items);

        var payload = items
            .Select(item => new JsonBatchItem(
                item.Url,
                item.Result.IsSuccess,
                item.Result.Content,
                item.Result.ProviderId,
                item.Result.ErrorMessage,
                item.Result.ModelName,
                ToUsage(item.Result),
                item.Result.Duration?.TotalMilliseconds))
            .ToList();

        return lines
            ? string.Join('\n', payload.Select(item => JsonSerializer.Serialize(item, CompactOptions)))
            : JsonSerializer.Serialize(payload, Options);
    }

    private static JsonSummary ToPayload(SummaryResult result) =>
        new(
            result.IsSuccess,
            result.Content,
            result.ProviderId,
            result.ErrorMessage,
            result.ModelName,
            ToUsage(result),
            result.Duration?.TotalMilliseconds);

    private static JsonUsage? ToUsage(SummaryResult result) =>
        result.Usage is null
            ? null
            : new JsonUsage(
                result.Usage.PromptTokens,
                result.Usage.CompletionTokens,
                result.Usage.TotalTokens);

    private sealed record JsonSummary(
        bool IsSuccess,
        string Content,
        string? ProviderId,
        string? ErrorMessage,
        string? ModelName,
        JsonUsage? Usage,
        double? DurationMs);

    private sealed record JsonBatchItem(
        string Url,
        bool IsSuccess,
        string Content,
        string? ProviderId,
        string? ErrorMessage,
        string? ModelName,
        JsonUsage? Usage,
        double? DurationMs);

    private sealed record JsonUsage(int PromptTokens, int CompletionTokens, int TotalTokens);
}
