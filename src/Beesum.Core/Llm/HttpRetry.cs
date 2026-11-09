using System.Net;

namespace Beesum.Core.Llm;

/// <summary>
/// Retries a request when the server says "later" (429) or "we are broken" (5xx).
/// The request is rebuilt per attempt because an <see cref="HttpRequestMessage"/>
/// cannot be sent twice.
/// </summary>
internal static class HttpRetry
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(5);

    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        Func<HttpRequestMessage> createRequest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(createRequest);

        for (var attempt = 1; ; attempt++)
        {
            using var request = createRequest();
            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (attempt == MaxAttempts || !IsTransient(response.StatusCode))
                return response;

            var delay = GetDelay(response, attempt);
            response.Dispose();

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status == HttpStatusCode.TooManyRequests || (int)status >= 500;

    private static TimeSpan GetDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } retryAfter)
            return retryAfter < MaxDelay ? retryAfter : MaxDelay;

        // 200ms, 400ms, ... — enough for a hiccup, short enough to not feel hung.
        var seconds = 0.2 * Math.Pow(2, attempt - 1);
        return TimeSpan.FromSeconds(seconds);
    }
}
