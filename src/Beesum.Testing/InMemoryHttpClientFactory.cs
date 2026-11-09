using System.Net.Http;

namespace Beesum.Testing;

/// <summary>Minimal <see cref="IHttpClientFactory"/> bound to a single handler.</summary>
public sealed class InMemoryHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    /// <summary>Creates a factory that returns clients bound to <paramref name="handler"/>.</summary>
    /// <param name="handler">Handler shared by all created clients.</param>
    public InMemoryHttpClientFactory(HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
    }

    /// <inheritdoc />
    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}
