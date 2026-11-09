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

/// <summary>Google Gemini via <c>generateContent</c>.</summary>
internal sealed class GeminiLlmProvider : ILlmProvider
{

    public string Id => "gemini";

    public string DisplayName => "Google Gemini";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly Func<CancellationToken, Task<string?>> _apiKeyProvider;
    private readonly int _maxTokens;
    private readonly ILogger<GeminiLlmProvider> _logger;
    private readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _summarizeTimeout = TimeSpan.FromMinutes(2);

    public GeminiLlmProvider(
        IHttpClientFactory httpClientFactory,
        string baseUrl,
        string model,
        Func<CancellationToken, Task<string?>> apiKeyProvider,
        int maxTokens = 1024,
        ILogger<GeminiLlmProvider>? logger = null)
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
        _logger = logger ?? NullLogger<GeminiLlmProvider>.Instance;
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
            var client = _httpClientFactory.CreateClient("gemini");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/v1beta/models");
            AddAuth(request, key);
            using var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new LlmAvailability(false, "invalid API key", _model);
            if (!response.IsSuccessStatusCode)
                return new LlmAvailability(false, $"HTTP {(int)response.StatusCode}", _model);

            var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("models", out var models) ||
                models.ValueKind != JsonValueKind.Array)
            {
                return new LlmAvailability(false, "Unexpected /v1beta/models response.", _model);
            }

            var expected = _model.StartsWith("models/", StringComparison.Ordinal)
                ? _model
                : "models/" + _model;

            var present = models.EnumerateArray().Any(m =>
                m.TryGetProperty("name", out var name) &&
                name.ValueKind == JsonValueKind.String &&
                string.Equals(name.GetString(), expected, StringComparison.Ordinal));

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
            var client = _httpClientFactory.CreateClient("gemini");
            using var response = await HttpRetry.SendAsync(
                client,
                () => CreateSummarizeRequest(key, request),
                cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                _logger.LogWarning("Gemini failed with HTTP {StatusCode}.", (int)response.StatusCode);
                return new SummaryResult(false, string.Empty, Id, ProviderSupport.DescribeHttpError(response.StatusCode, body));
            }

            var responseBody = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var text = ReadText(root);
            if (text is null)
                return new SummaryResult(false, string.Empty, Id, DescribeEmptyResponse(root));

            stopwatch.Stop();
            var model = ProviderSupport.TryGetString(root, "modelVersion") ?? _model;

            return new SummaryResult(
                true,
                text,
                Id,
                ModelName: model,
                Usage: ReadUsage(root),
                Duration: stopwatch.Elapsed);
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
        var model = _model.StartsWith("models/", StringComparison.Ordinal)
            ? _model["models/".Length..]
            : _model;

        var payload = new GeminiRequest(
            new GeminiSystemInstruction(new[]
            {
                new GeminiPart(LlmPrompts.BuildSystemPrompt(request)),
            }),
            new[]
            {
                new GeminiContent("user", new[] { new GeminiPart(request.Transcript.ToPlainText(request.Timestamps)) }),
            },
            new GeminiGenerationConfig(_maxTokens));

        var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_baseUrl}/v1beta/models/{model}:generateContent")
        {
            Content = JsonContent.Create(payload),
        };
        AddAuth(httpRequest, key);
        return httpRequest;
    }

    private static void AddAuth(HttpRequestMessage request, string key) =>
        request.Headers.Add("x-goog-api-key", key);

    private static string? ReadText(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
        {
            return null;
        }

        var first = candidates[0];
        if (!first.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                builder.Append(text.GetString());
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private static TokenUsage? ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usageMetadata", out var usage) ||
            usage.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!usage.TryGetProperty("promptTokenCount", out var prompt) ||
            !prompt.TryGetInt32(out var promptTokens) ||
            !usage.TryGetProperty("candidatesTokenCount", out var completion) ||
            !completion.TryGetInt32(out var completionTokens))
        {
            return null;
        }

        var total = usage.TryGetProperty("totalTokenCount", out var totalProp) &&
                    totalProp.TryGetInt32(out var totalTokens)
            ? totalTokens
            : promptTokens + completionTokens;

        return new TokenUsage(promptTokens, completionTokens, total);
    }

    private static string DescribeEmptyResponse(JsonElement root)
    {
        if (root.TryGetProperty("promptFeedback", out var feedback) &&
            feedback.TryGetProperty("blockReason", out var reason) &&
            reason.ValueKind == JsonValueKind.String)
        {
            return $"Prompt blocked: {reason.GetString()}";
        }

        return "Unexpected generateContent response.";
    }


    private sealed record GeminiRequest(
        [property: JsonPropertyName("system_instruction")] GeminiSystemInstruction SystemInstruction,
        [property: JsonPropertyName("contents")] IReadOnlyList<GeminiContent> Contents,
        [property: JsonPropertyName("generationConfig")] GeminiGenerationConfig GenerationConfig);

    private sealed record GeminiSystemInstruction(
        [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart> Parts);

    private sealed record GeminiContent(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart> Parts);

    private sealed record GeminiPart(
        [property: JsonPropertyName("text")] string Text);

    private sealed record GeminiGenerationConfig(
        [property: JsonPropertyName("maxOutputTokens")] int MaxOutputTokens);
}
