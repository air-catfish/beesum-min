using Beesum.Core.Models;

namespace Beesum.Core.Abstractions;

/// <summary>Parses WebVTT subtitle files into a <see cref="Transcript"/>.</summary>
public interface ISubtitleParser
{
    /// <summary>Parses WebVTT content from a string.</summary>
    Transcript Parse(string content, string sourceId, string? language = null);

    /// <summary>Reads and parses a WebVTT file from disk.</summary>
    Task<Transcript> ParseFileAsync(
        string path,
        string sourceId,
        string? language = null,
        CancellationToken cancellationToken = default);
}
