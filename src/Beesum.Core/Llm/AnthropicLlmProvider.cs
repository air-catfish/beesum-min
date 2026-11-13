using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Beesum.Core.Abstractions;
using Beesum.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Beesum.Core.Llm;

/// <summary>Claude via the Messages API.</summary>
internal sealed class AnthropicLlmProvider : ILlmProvider
{
    private const string ApiVersion = "2023-06-01";

    public string Id => "anthropic";

    public string DisplayName => "Anthropic (Claude)";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly Func<CancellationToken, Task<string?>> _apiKeyProvider;
    private readonly int _maxTokens;
    private readonly ILogger<AnthropicLlmProvider> _logger;
    private readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _summarizeTimeout = TimeSpan.FromMinutes(2);

    public AnthropicLlmProvider(
        IHttpClientFactory httpClientFactory,
        string baseUrl,
        string model,
        Func<CancellationToken, Task<string?>> apiKeyProvider,
        int maxTokens = 1024,
        ILogger<AnthropicLlmProvider>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(apiKeyProvider);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTokens);

        _httpClientFactory = httpClientFactory;
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
        _apiKeyProvider = apiKeyProvider;
        _maxTokens = maxTokens;
        _logger = logger ?? NullLogger<AnthropicLlmProvider>.Instance;
    }

    public async Task<LlmAvailability> ProbeAsync(CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_probeTimeout);

        var keyLookup = await ProviderSupport
            .ResolveKeyAsync(_apiKeyProvider, cts.Token, cancellationToken)
            .ConfigureAwait(false);
        if (keyLookup.Error is not null)
            return new LlmAvailability(false, keyLookup.Error, _model);

        var key = keyLookup.Key;
        if (string.IsNullOrEmpty(key))
            return new LlmAvailability(false, "API key not configured", _model);

        try
        {
            var client = _httpClientFactory.CreateClient("anthropic");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/v1/models");
            AddAuth(request, key);
            using var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new LlmAvailability(false, "invalid API key", _model);
            if (!response.IsSuccessStatusCode)
                return new LlmAvailability(false, $"HTTP {(int)response.StatusCode}", _model);

            var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
            {
                return new LlmAvailability(false, "Unexpected /v1/models response.", _model);
            }

            var present = data.EnumerateArray().Any(m =>
                m.TryGetProperty("id", out var id) &&
                id.ValueKind == JsonValueKind.String &&
                IsSameModel(id.GetString()));

            return present
                ? new LlmAvailability(true, ModelName: _model)
                : new LlmAvailability(false, $"model '{_model}' not listed", _model);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            return new LlmAvailability(false, "Probe timed out.", _model);
        }
        catch (Exception ex)
        {
            return new LlmAvailability(false, ex.Message, _model);
        }
    }

    public async Task<SummaryResult> SummarizeAsync(
        SummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_summarizeTimeout);

        var keyLookup = await ProviderSupport
            .ResolveKeyAsync(_apiKeyProvider, cts.Token, cancellationToken)
            .ConfigureAwait(false);
        if (keyLookup.Error is not null)
            return new SummaryResult(false, string.Empty, Id, keyLookup.Error);

        var key = keyLookup.Key;
        if (string.IsNullOrEmpty(key))
            return new SummaryResult(false, string.Empty, Id, "API key not configured");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var client = _httpClientFactory.CreateClient("anthropic");
            using var response = await HttpRetry.SendAsync(
                client,
                () => CreateSummarizeRequest(key, request),
                cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                _logger.LogWarning("Anthropic failed with HTTP {StatusCode}.", (int)response.StatusCode);
                return new SummaryResult(false, string.Empty, Id, ProviderSupport.DescribeHttpError(response.StatusCode, body));
            }

            var responseBody = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var text = ReadText(root);
            if (text is null)
                return new SummaryResult(false, string.Empty, Id, "Unexpected /v1/messages response.");

            stopwatch.Stop();
            var model = ProviderSupport.TryGetString(root, "model") ?? _model;

            return new SummaryResult(
                true,
                text,
                Id,
                ModelName: model,
                Usage: ReadUsage(root),
                Duration: stopwatch.Elapsed)
            {
                Truncated = string.Equals(
                    ProviderSupport.TryGetString(root, "stop_reason"),
                    "max_tokens",
                    StringComparison.Ordinal),
            };
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            return new SummaryResult(false, string.Empty, Id, "Summarize timed out.");
        }
        catch (Exception ex)
        {
            return new SummaryResult(false, string.Empty, Id, ex.Message);
        }
    }

    private HttpRequestMessage CreateSummarizeRequest(string key, SummaryRequest request)
    {
        var payload = new AnthropicRequest(
            _model,
            _maxTokens,
            LlmPrompts.BuildSystemPrompt(request),
            new[]
            {
                new AnthropicMessage("user", request.Transcript.ToPlainText(request.Timestamps)),
            });

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/messages")
        {
            Content = JsonContent.Create(payload),
        };
        AddAuth(httpRequest, key);
        return httpRequest;
    }

    private static void AddAuth(HttpRequestMessage request, string key)
    {
        request.Headers.Add("x-api-key", key);
        request.Headers.Add("anthropic-version", ApiVersion);
    }

    private bool IsSameModel(string? candidate) =>
        candidate is not null &&
        (string.Equals(candidate, _model, StringComparison.Ordinal) ||
         candidate.StartsWith(_model, StringComparison.Ordinal) ||
         _model.StartsWith(candidate, StringComparison.Ordinal));

    private static string? ReadText(JsonElement root)
    {
        if (!root.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var part in content.EnumerateArray())
        {
            if (part.TryGetProperty("type", out var type) &&
                type.ValueKind == JsonValueKind.String &&
                type.GetString() == "text" &&
                part.TryGetProperty("text", out var text) &&
                text.ValueKind == JsonValueKind.String)
            {
                builder.Append(text.GetString());
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private static TokenUsage? ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) ||
            usage.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!usage.TryGetProperty("input_tokens", out var input) ||
            !input.TryGetInt32(out var promptTokens) ||
            !usage.TryGetProperty("output_tokens", out var output) ||
            !output.TryGetInt32(out var completionTokens))
        {
            return null;
        }

        return TokenUsage.FromCounts(promptTokens, completionTokens);
    }


    private sealed record AnthropicRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("messages")] IReadOnlyList<AnthropicMessage> Messages);

    private sealed record AnthropicMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);
}
