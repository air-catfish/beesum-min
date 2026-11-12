using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Beesum.Core.Abstractions;
using Beesum.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Beesum.Core.Llm;

/// <summary>
/// Ollama-backed <see cref="ILlmProvider"/> using <c>/api/chat</c> with streaming disabled.
/// </summary>
internal sealed class OllamaLlmProvider : ILlmProvider
{
    /// <inheritdoc />
    public string Id => "ollama";

    /// <inheritdoc />
    public string DisplayName => "Ollama (local)";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly int _contextTokens;
    private readonly ILogger<OllamaLlmProvider> _logger;
    private readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(5);
    // Local models on CPU can need several minutes per chunk, unlike cloud APIs.
    private readonly TimeSpan _summarizeTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Creates a provider targeting an Ollama server.
    /// </summary>
    /// <param name="httpClientFactory">Factory that yields the HTTP client used for calls.</param>
    /// <param name="baseUrl">Base URL, e.g. <c>http://localhost:11434</c>.</param>
    /// <param name="model">Model name, e.g. <c>llama3.2</c>.</param>
    /// <param name="logger">Optional logger; <c>null</c> uses a no-op logger.</param>
    /// <param name="contextTokens">
    /// Context window requested from Ollama (<c>num_ctx</c>). Without it Ollama uses its own
    /// default, which can be too small and silently cuts the start of a long transcript.
    /// </param>
    public OllamaLlmProvider(
        IHttpClientFactory httpClientFactory,
        string baseUrl = "http://localhost:11434",
        string model = "llama3.2",
        ILogger<OllamaLlmProvider>? logger = null,
        int contextTokens = 8192)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contextTokens);
        _httpClientFactory = httpClientFactory;
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
        _contextTokens = contextTokens;
        _logger = logger ?? NullLogger<OllamaLlmProvider>.Instance;
    }

    /// <inheritdoc />
    public async Task<LlmAvailability> ProbeAsync(CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_probeTimeout);

        try
        {
            var client = _httpClientFactory.CreateClient("ollama");
            using var response = await client
                .GetAsync($"{_baseUrl}/api/tags", cts.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Ollama probe failed with HTTP {StatusCode}.", (int)response.StatusCode);
                return new LlmAvailability(false, $"HTTP {(int)response.StatusCode}");
            }

            var body = await response.Content
                .ReadAsStringAsync(cts.Token)
                .ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("models", out var models) ||
                models.ValueKind != JsonValueKind.Array)
            {
                return new LlmAvailability(false, "Unexpected /api/tags response.");
            }

            var present = models.EnumerateArray().Any(m =>
                m.TryGetProperty("name", out var name) &&
                name.ValueKind == JsonValueKind.String &&
                IsSameModel(name.GetString(), _model));

            _logger.LogDebug(
                "Ollama probe for {Model}: {Status}.",
                _model,
                present ? "available" : "not pulled");

            return present
                ? new LlmAvailability(true, ModelName: _model)
                : new LlmAvailability(false, $"model '{_model}' not pulled", _model);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            return new LlmAvailability(false, "Probe timed out.");
        }
        catch (Exception ex)
        {
            return new LlmAvailability(false, ex.Message);
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

        try
        {
            var client = _httpClientFactory.CreateClient("ollama");
            var payload = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = LlmPrompts.BuildSystemPrompt(request) },
                    new { role = "user", content = request.Transcript.ToPlainText(request.Timestamps) },
                },
                stream = false,
                options = new { num_ctx = _contextTokens },
            };

            var stopwatch = Stopwatch.StartNew();
            using var response = await client
                .PostAsJsonAsync($"{_baseUrl}/api/chat", payload, cts.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content
                    .ReadAsStringAsync(cts.Token)
                    .ConfigureAwait(false);
                _logger.LogWarning("Ollama summarize failed with HTTP {StatusCode}.", (int)response.StatusCode);
                return new SummaryResult(false, string.Empty, Id, $"HTTP {(int)response.StatusCode}: {err}");
            }

            var body = await response.Content
                .ReadAsStringAsync(cts.Token)
                .ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.String)
            {
                return new SummaryResult(false, string.Empty, Id, "Unexpected /api/chat response.");
            }

            stopwatch.Stop();
            var modelName = TryGetString(doc.RootElement, "model") ?? _model;
            var usage = ReadUsage(doc.RootElement);

            var outputCut = string.Equals(
                TryGetString(doc.RootElement, "done_reason"),
                "length",
                StringComparison.Ordinal);

            // Ollama silently drops the start of a prompt that does not fit num_ctx, and the
            // only hint is a prompt token count at the limit.
            var inputCut = usage is not null && usage.PromptTokens >= _contextTokens - 16;
            if (outputCut || inputCut)
            {
                _logger.LogWarning(
                    "Ollama reply may be incomplete (output cut: {OutputCut}, input cut: {InputCut}, num_ctx {NumCtx}).",
                    outputCut,
                    inputCut,
                    _contextTokens);
            }

            return new SummaryResult(
                true,
                content.GetString() ?? string.Empty,
                Id,
                ModelName: modelName,
                Usage: usage,
                Duration: stopwatch.Elapsed)
            {
                Truncated = outputCut || inputCut,
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

    private static bool IsSameModel(string? candidate, string target)
    {
        if (string.IsNullOrEmpty(candidate)) return false;
        if (string.Equals(candidate, target, StringComparison.Ordinal)) return true;
        // Ollama appends :latest to bare model names.
        return string.Equals(candidate, target + ":latest", StringComparison.Ordinal);
    }

    private static string? TryGetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static TokenUsage? ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("prompt_eval_count", out var prompt) ||
            !prompt.TryGetInt32(out var promptTokens) ||
            !root.TryGetProperty("eval_count", out var completion) ||
            !completion.TryGetInt32(out var completionTokens))
        {
            return null;
        }

        return TokenUsage.FromCounts(promptTokens, completionTokens);
    }
}
