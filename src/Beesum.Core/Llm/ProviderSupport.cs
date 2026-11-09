using System.Net;
using System.Text.Json;

namespace Beesum.Core.Llm;

/// <summary>Result of reading an API key: the key may be null, the error is not.</summary>
internal sealed record ApiKeyLookup(string? Key, string? Error);

internal static class ProviderSupport
{
    /// <summary>
    /// Reads the key. A canceled probe is a timeout only when the caller did not
    /// cancel: the caller's token is checked separately on purpose.
    /// </summary>
    public static async Task<ApiKeyLookup> ResolveKeyAsync(
        Func<CancellationToken, Task<string?>> resolve,
        CancellationToken token,
        CancellationToken callerToken)
    {
        try
        {
            return new ApiKeyLookup(await resolve(token).ConfigureAwait(false), null);
        }
        catch (OperationCanceledException)
        {
            if (callerToken.IsCancellationRequested) throw;
            return new ApiKeyLookup(null, "Timed out resolving API key.");
        }
        catch (Exception ex)
        {
            return new ApiKeyLookup(null, $"Failed to read API key: {ex.Message}");
        }
    }

    public static string DescribeHttpError(HttpStatusCode status, string body) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "invalid API key",
        HttpStatusCode.TooManyRequests => "rate limited; retry later",
        _ => $"HTTP {(int)status}: {Truncate(body)}",
    };

    public static string Truncate(string value, int limit = 300) =>
        value.Length <= limit ? value : value[..limit] + "...";

    public static string? TryGetString(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
