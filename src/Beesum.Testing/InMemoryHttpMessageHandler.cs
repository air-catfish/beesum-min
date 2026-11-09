using System.Net;
using System.Text;

namespace Beesum.Testing;

/// <summary>
/// <see cref="HttpMessageHandler"/> that returns queued responses and records requests.
/// </summary>
public sealed class InMemoryHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage?> _responses = new();
    private readonly Queue<Exception?> _exceptions = new();

    /// <summary>Requests observed by this handler, in order.</summary>
    public List<HttpRequestMessage> Requests { get; } = new();

    /// <summary>Bodies of requests, in order, as strings.</summary>
    public List<string> RequestBodies { get; } = new();

    /// <summary>Queues a raw response.</summary>
    /// <param name="response">Response to return.</param>
    public InMemoryHttpMessageHandler Enqueue(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        _responses.Enqueue(response);
        _exceptions.Enqueue(null);
        return this;
    }

    /// <summary>Queues a JSON response.</summary>
    /// <param name="json">Response body.</param>
    /// <param name="statusCode">HTTP status code.</param>
    public InMemoryHttpMessageHandler EnqueueJson(string json, int statusCode = 200)
    {
        var response = new HttpResponseMessage((HttpStatusCode)statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        return Enqueue(response);
    }

    /// <summary>Queues an exception to throw instead of a response.</summary>
    /// <param name="exception">Exception to throw.</param>
    public InMemoryHttpMessageHandler EnqueueException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _responses.Enqueue(null);
        _exceptions.Enqueue(exception);
        return this;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        RequestBodies.Add(request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

        if (_responses.Count == 0)
            throw new InvalidOperationException("InMemoryHttpMessageHandler has no queued response.");

        var exception = _exceptions.Dequeue();
        var response = _responses.Dequeue();
        if (exception is not null) throw exception;
        return response!;
    }
}
