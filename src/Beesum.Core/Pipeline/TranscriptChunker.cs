using System.Text;
using Beesum.Core.Models;

namespace Beesum.Core.Pipeline;

/// <summary>
/// Splits a transcript into chunks that fit the provider's input budget. Segment
/// boundaries are preferred; a single oversized segment is cut on word boundaries.
/// </summary>
internal static class TranscriptChunker
{
    public static IReadOnlyList<Transcript> Split(Transcript transcript, int maxTokens)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTokens);

        if (transcript.Segments.Count == 0)
            return new[] { transcript };

        var chunks = new List<Transcript>();
        var current = new List<SubtitleSegment>();
        var currentTokens = 0;

        void Flush()
        {
            if (current.Count == 0) return;

            chunks.Add(Create(transcript, current));
            current = new List<SubtitleSegment>();
            currentTokens = 0;
        }

        foreach (var segment in transcript.Segments)
        {
            var tokens = TokenEstimator.Estimate(segment.Text);
            if (tokens > maxTokens)
            {
                Flush();
                foreach (var piece in SplitText(segment.Text, maxTokens))
                {
                    chunks.Add(Create(transcript, new[]
                    {
                        new SubtitleSegment(segment.Start, segment.End, piece),
                    }));
                }

                continue;
            }

            if (currentTokens + tokens > maxTokens)
                Flush();

            current.Add(segment);
            currentTokens += tokens;
        }

        Flush();

        return chunks.Count == 0 ? new[] { transcript } : chunks;
    }

    private static Transcript Create(Transcript source, IEnumerable<SubtitleSegment> segments) =>
        new(source.SourceId, source.Language, segments.ToArray());

    private static IEnumerable<string> SplitText(string text, int maxTokens)
    {
        var maxChars = Math.Max(1, maxTokens * 3);
        if (text.Length <= maxChars)
        {
            yield return text;
            yield break;
        }

        var start = 0;
        while (start < text.Length)
        {
            var length = Math.Min(maxChars, text.Length - start);

            if (start + length < text.Length)
            {
                // Do not cut a word in half when there is a space to cut at.
                var lastSpace = text.LastIndexOf(' ', start + length - 1, length);
                if (lastSpace > start)
                    length = lastSpace - start;
            }

            var piece = text.Substring(start, length).Trim();
            if (piece.Length > 0)
                yield return piece;

            start += length;
            while (start < text.Length && char.IsWhiteSpace(text[start]))
                start++;
        }
    }
}
