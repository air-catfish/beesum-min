using Beesum.Core.Abstractions;
using Beesum.Core.Models;

namespace Beesum.Testing;

/// <summary>Configurable <see cref="ISubtitleParser"/> for tests.</summary>
public sealed class InMemorySubtitleParser : ISubtitleParser
{
    /// <summary>Calls to <see cref="ParseFileAsync"/>, in order.</summary>
    public List<(string Path, string SourceId, string? Language)> ParseFileCalls { get; } = new();

    /// <summary>Calls to <see cref="Parse"/>, in order.</summary>
    public List<(string SourceId, string? Language)> ParseCalls { get; } = new();

    /// <summary>Result returned by both parse methods.</summary>
    public Transcript ParseResult { get; set; } =
        new("src", "en", Array.Empty<SubtitleSegment>());

    /// <inheritdoc />
    public Transcript Parse(string content, string sourceId, string? language = null)
    {
        ParseCalls.Add((sourceId, language));
        return ParseResult;
    }

    /// <inheritdoc />
    public Task<Transcript> ParseFileAsync(
        string path,
        string sourceId,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ParseFileCalls.Add((path, sourceId, language));
        return Task.FromResult(ParseResult);
    }
}
