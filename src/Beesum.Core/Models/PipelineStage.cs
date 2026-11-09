namespace Beesum.Core.Models;

/// <summary>Progress stages reported by <see cref="Abstractions.ISummarizerPipeline"/>.</summary>
public enum PipelineStage
{
    /// <summary>Pipeline is starting.</summary>
    Starting,
    /// <summary>Subtitles are being downloaded.</summary>
    Downloading,
    /// <summary>Subtitles are being parsed.</summary>
    Parsing,
    /// <summary>Long transcript is being split into chunks.</summary>
    Chunking,
    /// <summary>Transcript is being summarized.</summary>
    Summarizing,
    /// <summary>Pipeline finished successfully.</summary>
    Completed
}
