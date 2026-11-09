using Beesum.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Beesum.Core.Pipeline;

/// <inheritdoc cref="ISummarizerPipelineFactory" />
internal sealed class SummarizerPipelineFactory : ISummarizerPipelineFactory
{
    private readonly IYtDlpService _ytDlp;
    private readonly ISubtitleParser _parser;
    private readonly IReadOnlyList<ILlmProvider> _providers;
    private readonly string? _subtitlesDirectory;
    private readonly ILogger<SummarizerPipeline>? _logger;
    private readonly bool _chunkingEnabled;
    private readonly int _chunkTokens;

    public SummarizerPipelineFactory(
        IYtDlpService ytDlp,
        ISubtitleParser parser,
        IEnumerable<ILlmProvider> providers,
        string? subtitlesDirectory = null,
        ILogger<SummarizerPipeline>? logger = null,
        bool chunkingEnabled = true,
        int chunkTokens = 6000)
    {
        ArgumentNullException.ThrowIfNull(ytDlp);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(providers);

        _ytDlp = ytDlp;
        _parser = parser;
        _providers = providers.ToList();
        _subtitlesDirectory = subtitlesDirectory;
        _logger = logger;
        _chunkingEnabled = chunkingEnabled;
        _chunkTokens = chunkTokens;
    }

    /// <inheritdoc />
    public ISummarizerPipeline Create(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);

        return new SummarizerPipeline(
            _ytDlp,
            _parser,
            _providers,
            providerId,
            _subtitlesDirectory,
            _logger,
            _chunkingEnabled,
            _chunkTokens);
    }
}
