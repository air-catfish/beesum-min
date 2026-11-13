using Beesum.Core.Abstractions;
using Beesum.Core.Llm;
using Beesum.Core.Parsing;
using Beesum.Core.Pipeline;
using Beesum.Core.Processes;
using Beesum.Core.Storage;
using Beesum.Core.YtDlp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Beesum.Core.DependencyInjection;

/// <summary>DI registration for the Core layer.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Core contracts and their default implementations.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">
    /// Optional configuration callback. When <c>null</c>, defaults are used:
    /// transcript-only provider, Ollama at <c>http://localhost:11434</c>, and
    /// auto-detected yt-dlp.
    /// </param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// The structure of the container (which providers exist, where yt-dlp comes
    /// from) is decided at registration time from the values set by
    /// <paramref name="configure"/>. The same values are exposed afterwards as
    /// <c>IOptions&lt;BeesumOptions&gt;</c>.
    /// </para>
    /// <para>
    /// Logging is optional: when <c>AddLogging()</c> is registered, Core services
    /// use the supplied <see cref="ILogger{T}"/>; otherwise they log to
    /// <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddBeesum(options =>
    /// {
    ///     options.LlmProviderId = "ollama";
    ///     options.OllamaModel = "llama3.2";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddBeesum(
        this IServiceCollection services,
        Action<BeesumOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new BeesumOptions();
        configure?.Invoke(options);
        options.Validate();

        return AddBeesumServices(services, options);
    }

    /// <summary>
    /// Registers Core contracts using the legacy positional API.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="ytDlpPath">Optional explicit yt-dlp path; <c>null</c> auto-detects.</param>
    /// <param name="ollamaBaseUrl">Ollama base URL; <c>null</c> skips Ollama registration.</param>
    /// <param name="ollamaModel">Ollama model name.</param>
    /// <param name="openAiBaseUrl">OpenAI-compatible base URL; <c>null</c> skips registration.</param>
    /// <param name="openAiModel">OpenAI-compatible model name.</param>
    /// <param name="openAiApiKeyProvider">Callback resolving the API key.</param>
    /// <param name="llmProviderId">Id of the LLM provider used by the pipeline.</param>
    /// <param name="subtitlesDirectory">Optional persistent subtitle directory.</param>
    /// <param name="storageRoot">Optional storage root.</param>
    /// <param name="ytDlpExtraArgs">Extra arguments forwarded to every yt-dlp call.</param>
    /// <returns>The same service collection, for chaining.</returns>
    [Obsolete("Use AddBeesum(Action<BeesumOptions>) instead.")]
    public static IServiceCollection AddBeesumCore(
        this IServiceCollection services,
        string? ytDlpPath = null,
        string? ollamaBaseUrl = "http://localhost:11434",
        string ollamaModel = "llama3.2",
        string? openAiBaseUrl = null,
        string openAiModel = "gpt-4o-mini",
        Func<CancellationToken, Task<string?>>? openAiApiKeyProvider = null,
        string llmProviderId = "transcript-only",
        string? subtitlesDirectory = null,
        string? storageRoot = null,
        IReadOnlyList<string>? ytDlpExtraArgs = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new BeesumOptions
        {
            YtDlpPath = ytDlpPath,
            OllamaBaseUrl = ollamaBaseUrl,
            OllamaModel = ollamaModel,
            OpenAiBaseUrl = openAiBaseUrl,
            OpenAiModel = openAiModel,
            OpenAiApiKeyProvider = openAiApiKeyProvider,
            LlmProviderId = llmProviderId,
            SubtitlesDirectory = subtitlesDirectory,
            StorageRoot = storageRoot,
        };

        if (ytDlpExtraArgs is not null)
        {
            foreach (var arg in ytDlpExtraArgs)
                options.YtDlpExtraArgs.Add(arg);
        }

        options.Validate();
        return AddBeesumServices(services, options);
    }

    /// <summary>
    /// Providers enforce their own time limits with a cancellation token, so the default
    /// 100-second <see cref="HttpClient.Timeout"/> must not cut a request off first.
    /// </summary>
    private static void NoClientTimeout(HttpClient client) =>
        client.Timeout = Timeout.InfiniteTimeSpan;

    private static IServiceCollection AddBeesumServices(
        IServiceCollection services,
        BeesumOptions options)
    {
        services.AddOptions<BeesumOptions>().Configure(options.CopyTo);

        var paths = options.StorageRoot is null
            ? StoragePaths.Default()
            : new StoragePaths(options.StorageRoot);

        services.AddSingleton<ISettingsStore>(_ => new FileSettingsStore(paths.SettingsFile));
        services.AddSingleton<ISecretStore>(_ => new DpapiSecretStore(paths.SecretsFile));
        services.AddSingleton<ITranscriptCache>(_ => new FileTranscriptCache(paths.TranscriptsDirectory));
        services.AddSingleton<ISummaryHistory>(_ => new JsonSummaryHistory(paths.HistoryFile));

        services.AddSingleton<ISubtitleParser, VttParser>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();

        if (options.YtDlpPath is null)
        {
            services.AddSingleton<IYtDlpPathResolver>(sp =>
                new YtDlpPathResolver(
                    sp.GetRequiredService<ISettingsStore>(),
                    logger: sp.GetService<ILogger<YtDlpPathResolver>>()));
        }
        else
        {
            services.AddSingleton<IYtDlpPathResolver>(_ => new StaticYtDlpPathResolver(options.YtDlpPath));
        }

        services.AddSingleton<IYtDlpService>(sp =>
            new YtDlpService(
                sp.GetRequiredService<IProcessRunner>(),
                sp.GetRequiredService<IYtDlpPathResolver>(),
                extraArgs: options.YtDlpExtraArgs.ToArray(),
                logger: sp.GetService<ILogger<YtDlpService>>(),
                subtitleLanguages: options.SubtitleLanguages.ToArray()));

        services.AddSingleton<ILlmProvider, TranscriptOnlyLlmProvider>();

        if (!string.IsNullOrWhiteSpace(options.OllamaBaseUrl))
        {
            services.AddHttpClient("ollama").ConfigureHttpClient(NoClientTimeout);
            services.AddSingleton<ILlmProvider>(sp =>
                new OllamaLlmProvider(
                    sp.GetRequiredService<IHttpClientFactory>(),
                    options.OllamaBaseUrl,
                    options.OllamaModel,
                    sp.GetService<ILogger<OllamaLlmProvider>>(),
                    Math.Max(options.OllamaContextTokens, options.ChunkTokens + 2048)));
        }

        if (!string.IsNullOrWhiteSpace(options.OpenAiBaseUrl))
        {
            services.AddHttpClient("openai").ConfigureHttpClient(NoClientTimeout);
            services.AddSingleton<ILlmProvider>(sp =>
            {
                var keyProvider = options.OpenAiApiKeyProvider ?? (async ct =>
                    await sp.GetRequiredService<ISecretStore>()
                        .GetAsync("openai.apiKey", ct)
                        .ConfigureAwait(false));

                return new OpenAiCompatibleLlmProvider(
                    sp.GetRequiredService<IHttpClientFactory>(),
                    options.OpenAiBaseUrl,
                    options.OpenAiModel,
                    keyProvider,
                    sp.GetService<ILogger<OpenAiCompatibleLlmProvider>>());
            });
        }

        if (!string.IsNullOrWhiteSpace(options.AnthropicBaseUrl))
        {
            services.AddHttpClient("anthropic").ConfigureHttpClient(NoClientTimeout);
            services.AddSingleton<ILlmProvider>(sp =>
            {
                var keyProvider = options.AnthropicApiKeyProvider ?? (async ct =>
                    await sp.GetRequiredService<ISecretStore>()
                        .GetAsync("anthropic.apiKey", ct)
                        .ConfigureAwait(false));

                return new AnthropicLlmProvider(
                    sp.GetRequiredService<IHttpClientFactory>(),
                    options.AnthropicBaseUrl,
                    options.AnthropicModel,
                    keyProvider,
                    options.MaxOutputTokens,
                    sp.GetService<ILogger<AnthropicLlmProvider>>());
            });
        }

        if (!string.IsNullOrWhiteSpace(options.GeminiBaseUrl))
        {
            services.AddHttpClient("gemini").ConfigureHttpClient(NoClientTimeout);
            services.AddSingleton<ILlmProvider>(sp =>
            {
                var keyProvider = options.GeminiApiKeyProvider ?? (async ct =>
                    await sp.GetRequiredService<ISecretStore>()
                        .GetAsync("gemini.apiKey", ct)
                        .ConfigureAwait(false));

                return new GeminiLlmProvider(
                    sp.GetRequiredService<IHttpClientFactory>(),
                    options.GeminiBaseUrl,
                    options.GeminiModel,
                    keyProvider,
                    options.MaxOutputTokens,
                    sp.GetService<ILogger<GeminiLlmProvider>>());
            });
        }
        services.AddSingleton<ISummarizerPipelineFactory>(sp =>
            new SummarizerPipelineFactory(
                sp.GetRequiredService<IYtDlpService>(),
                sp.GetRequiredService<ISubtitleParser>(),
                sp.GetServices<ILlmProvider>(),
                options.SubtitlesDirectory,
                sp.GetService<ILogger<SummarizerPipeline>>(),
                options.ChunkLongTranscripts,
                options.ChunkTokens));

        services.AddSingleton<ISummarizerPipeline>(sp =>
            sp.GetRequiredService<ISummarizerPipelineFactory>()
                .Create(options.LlmProviderId));

        return services;
    }
}
