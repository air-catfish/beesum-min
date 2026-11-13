namespace Beesum.Core;

/// <summary>
/// Configuration for <see cref="DependencyInjection.ServiceCollectionExtensions.AddBeesum"/>.
/// </summary>
/// <remarks>
/// Read once, when the container is built. Mutating the object afterwards does not
/// re-register anything.
/// </remarks>
public sealed class BeesumOptions
{
    /// <summary>
    /// Explicit yt-dlp path. <c>null</c> auto-detects: settings → <c>tools/</c> → <c>PATH</c>.
    /// </summary>
    public string? YtDlpPath { get; set; }

    /// <summary>Ollama base URL; <c>null</c> or empty disables the provider.</summary>
    public string? OllamaBaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Ollama model name.</summary>
    public string OllamaModel { get; set; } = "llama3.2";

    /// <summary>
    /// Context window requested from Ollama (<c>num_ctx</c>). The effective value is never
    /// smaller than <see cref="ChunkTokens"/> plus 2048 so a chunk and its answer fit.
    /// </summary>
    public int OllamaContextTokens { get; set; } = 8192;

    /// <summary>OpenAI-compatible base URL; <c>null</c> or empty disables the provider.</summary>
    public string? OpenAiBaseUrl { get; set; }

    /// <summary>OpenAI-compatible model name.</summary>
    public string OpenAiModel { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Resolves the OpenAI-compatible key. <c>null</c> reads <c>openai.apiKey</c>
    /// from <see cref="Abstractions.ISecretStore"/>.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? OpenAiApiKeyProvider { get; set; }

    /// <summary>Anthropic base URL; <c>null</c> or empty disables the provider.</summary>
    public string? AnthropicBaseUrl { get; set; }

    /// <summary>Anthropic model, e.g. <c>claude-sonnet-4-5</c>.</summary>
    public string AnthropicModel { get; set; } = "claude-sonnet-4-5";

    /// <summary>
    /// Resolves the Anthropic key. <c>null</c> reads <c>anthropic.apiKey</c>
    /// from <see cref="Abstractions.ISecretStore"/>.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? AnthropicApiKeyProvider { get; set; }

    /// <summary>
    /// Gemini base URL, e.g. <c>https://generativelanguage.googleapis.com</c>.
    /// <c>null</c> or empty disables the provider.
    /// </summary>
    public string? GeminiBaseUrl { get; set; }

    /// <summary>Gemini model, e.g. <c>gemini-2.5-flash</c>.</summary>
    public string GeminiModel { get; set; } = "gemini-2.5-flash";

    /// <summary>
    /// Resolves the Gemini key. <c>null</c> reads <c>gemini.apiKey</c>
    /// from <see cref="Abstractions.ISecretStore"/>.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? GeminiApiKeyProvider { get; set; }

    /// <summary>Maximum output tokens for providers that require it (Anthropic, Gemini).</summary>
    public int MaxOutputTokens { get; set; } = 1024;

    /// <summary>Split long transcripts into chunks and combine the partial summaries.</summary>
    public bool ChunkLongTranscripts { get; set; } = true;

    /// <summary>Approximate input budget per chunk when chunking is enabled.</summary>
    public int ChunkTokens { get; set; } = 6000;

    /// <summary>Provider used by the default pipeline, e.g. <c>transcript-only</c>.</summary>
    public string LlmProviderId { get; set; } = "transcript-only";

    /// <summary>Persistent subtitle directory; <c>null</c> uses a temp directory.</summary>
    public string? SubtitlesDirectory { get; set; }

    /// <summary>Storage root for settings, secrets, cache, and history.</summary>
    public string? StorageRoot { get; set; }

    /// <summary>Extra arguments for every yt-dlp call.</summary>
    public IList<string> YtDlpExtraArgs { get; } = new List<string>();

    /// <summary>
    /// Subtitle language selectors passed to <c>--sub-langs</c>, e.g. <c>en</c>,
    /// <c>ru,en</c>, or <c>en.*</c> for translated variants. Defaults to <c>en</c>.
    /// </summary>
    public IList<string> SubtitleLanguages { get; } = new List<string> { "en" };

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(LlmProviderId, nameof(LlmProviderId));
        ArgumentException.ThrowIfNullOrWhiteSpace(OllamaModel, nameof(OllamaModel));
        ArgumentException.ThrowIfNullOrWhiteSpace(OpenAiModel, nameof(OpenAiModel));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxOutputTokens, nameof(MaxOutputTokens));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ChunkTokens, nameof(ChunkTokens));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(OllamaContextTokens, nameof(OllamaContextTokens));

        if (YtDlpPath is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(YtDlpPath, nameof(YtDlpPath));
        if (OllamaBaseUrl is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(OllamaBaseUrl, nameof(OllamaBaseUrl));
        if (OpenAiBaseUrl is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(OpenAiBaseUrl, nameof(OpenAiBaseUrl));
        if (AnthropicBaseUrl is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(AnthropicBaseUrl, nameof(AnthropicBaseUrl));
            ArgumentException.ThrowIfNullOrWhiteSpace(AnthropicModel, nameof(AnthropicModel));
        }

        if (GeminiBaseUrl is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(GeminiBaseUrl, nameof(GeminiBaseUrl));
            ArgumentException.ThrowIfNullOrWhiteSpace(GeminiModel, nameof(GeminiModel));
        }

        foreach (var arg in YtDlpExtraArgs)
        {
            if (arg is null)
                throw new ArgumentException("yt-dlp extra arguments must not contain null.", nameof(YtDlpExtraArgs));
        }

        if (SubtitleLanguages.Count == 0)
            throw new ArgumentException("At least one subtitle language is required.", nameof(SubtitleLanguages));

        foreach (var language in SubtitleLanguages)
        {
            if (string.IsNullOrWhiteSpace(language))
                throw new ArgumentException("Subtitle languages must not be blank.", nameof(SubtitleLanguages));
        }
    }

    internal void CopyTo(BeesumOptions target)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.YtDlpPath = YtDlpPath;
        target.OllamaBaseUrl = OllamaBaseUrl;
        target.OllamaModel = OllamaModel;
        target.OllamaContextTokens = OllamaContextTokens;
        target.OpenAiBaseUrl = OpenAiBaseUrl;
        target.OpenAiModel = OpenAiModel;
        target.OpenAiApiKeyProvider = OpenAiApiKeyProvider;
        target.AnthropicBaseUrl = AnthropicBaseUrl;
        target.AnthropicModel = AnthropicModel;
        target.AnthropicApiKeyProvider = AnthropicApiKeyProvider;
        target.GeminiBaseUrl = GeminiBaseUrl;
        target.GeminiModel = GeminiModel;
        target.GeminiApiKeyProvider = GeminiApiKeyProvider;
        target.MaxOutputTokens = MaxOutputTokens;
        target.ChunkLongTranscripts = ChunkLongTranscripts;
        target.ChunkTokens = ChunkTokens;
        target.LlmProviderId = LlmProviderId;
        target.SubtitlesDirectory = SubtitlesDirectory;
        target.StorageRoot = StorageRoot;

        target.YtDlpExtraArgs.Clear();
        foreach (var arg in YtDlpExtraArgs)
            target.YtDlpExtraArgs.Add(arg);

        target.SubtitleLanguages.Clear();
        foreach (var language in SubtitleLanguages)
            target.SubtitleLanguages.Add(language);
    }
}
