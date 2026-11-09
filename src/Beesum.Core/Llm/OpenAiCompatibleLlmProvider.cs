using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Beesum.Core.Abstractions;
using Beesum.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Beesum.Core.Llm;

/// <summary>
/// OpenAI-compatible <see cref="ILlmProvider"/> using <c>/v1/chat/completions</c>
/// (OpenAI, OpenRouter, Together, LM Studio, llama.cpp server, etc.).
/// </summary>
internal sealed class OpenAiCompatibleLlmProvider : ILlmProvider
{
    /// <inheritdoc />
    public string Id => "openai-compatible";

    /// <inheritdoc />
    public string DisplayName => "OpenAI-compatible API";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly Func<CancellationToken, Task<string?>> _apiKeyProvider;
    private readonly ILogger<OpenAiCompatibleLlmProvider> _logger;
    private readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _summarizeTimeout = TimeSpan.FromMinutes(2);

    /// <summary>Creates a provider targeting an OpenAI-compatible endpoint.</summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="baseUrl">Base URL, e.g. <c>https://api.openai.com</c> or <c>http://localhost:1234</c>.</param>
    /// <param name="model">Model name, e.g. <c>gpt-4o-mini</c>.</param>
    /// <param name="apiKeyProvider">Callback that resolves the API key (typically from <see cref="ISecretStore"/>).</param>
    /// <param name="logger">Optional logger; <c>null</c> uses a no-op logger.</param>
    public OpenAiCompatibleLlmProvider(
        IHttpClientFactory httpClientFactory,
        string baseUrl,
        string model,
        Func<CancellationToken, Task<string?>> apiKeyProvider,
        ILogger<OpenAiCompatibleLlmProvider>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(apiKeyProvider);
        _httpClientFactory = httpClientFactory;
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
        _apiKeyProvider = apiKeyProvider;
        _logger = logger ?? NullLogger<OpenAiCompatibleLlmProvider>.Instance;
    }

    /// <inheritdoc />
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

        try
        {
            var client = _httpClientFactory.CreateClient("openai");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/v1/models");
            if (!string.IsNullOrEmpty(key))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            using var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
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
                string.Equals(id.GetString(), _model, StringComparison.Ordinal));

            _logger.LogDebug(
                "OpenAI-compatible probe for {Model}: {Status}.",
                _model,
                present ? "available" : "not listed");

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

    /// <inheritdoc />
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

        try
        {
            var client = _httpClientFactory.CreateClient("openai");
            var payload = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = LlmPrompts.BuildSystemPrompt(request) },
                    new { role = "user", content = request.Transcript.ToPlainText(request.Timestamps) },
                },
                stream = false,
            };

            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_baseUrl}/v1/chat/completions")
            {
                Content = JsonContent.Create(payload),
            };
            if (!string.IsNullOrEmpty(key))
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

            var stopwatch = Stopwatch.StartNew();
            using var response = await client.SendAsync(httpRequest, cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                _logger.LogWarning(
                    "OpenAI-compatible summarize failed with HTTP {StatusCode}.",
                    (int)response.StatusCode);
                return new SummaryResult(
                    false,
                    string.Empty,
                    Id,
                    $"HTTP {(int)response.StatusCode}: {ProviderSupport.Truncate(err)}");
            }

            var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array)
            {
                return new SummaryResult(false, string.Empty, Id, "Unexpected /v1/chat/completions response.");
            }

            var first = choices.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Undefined ||
                !first.TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.String)
            {
                return new SummaryResult(false, string.Empty, Id, "Unexpected /v1/chat/completions response.");
            }

            stopwatch.Stop();
            var modelName = ProviderSupport.TryGetString(doc.RootElement, "model") ?? _model;

            return new SummaryResult(
                true,
                content.GetString() ?? string.Empty,
                Id,
                ModelName: modelName,
                Usage: ReadUsage(doc.RootElement),
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


    private static TokenUsage? ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
            return null;
        if (!usage.TryGetProperty("prompt_tokens", out var prompt) ||
            !prompt.TryGetInt32(out var promptTokens) ||
            !usage.TryGetProperty("completion_tokens", out var completion) ||
            !completion.TryGetInt32(out var completionTokens))
        {
            return null;
        }

        var total = usage.TryGetProperty("total_tokens", out var totalProp) &&
                    totalProp.TryGetInt32(out var totalTokens)
            ? totalTokens
            : promptTokens + completionTokens;

        return new TokenUsage(promptTokens, completionTokens, total);
    }
}
