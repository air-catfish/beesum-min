using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Beesum.Core.Abstractions;
using Beesum.Core.Models;

namespace Beesum.Core.Parsing;

/// <summary>
/// Parses WebVTT subtitle files, including YouTube's auto-generated rolling-window format.
/// </summary>
internal sealed partial class VttParser : ISubtitleParser
{
    private static readonly TimeSpan MarkerInterval = TimeSpan.FromMinutes(15);
    private const int MinCueDurationMs = 100;

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex VttTagRegex();

    /// <inheritdoc />
    public Transcript Parse(string content, string sourceId, string? language = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new Transcript(sourceId, language ?? string.Empty, Array.Empty<SubtitleSegment>());

        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalized.Split('\n');

        var cues = ParseCues(lines);
        var segments = ProcessCues(cues);

        return new Transcript(sourceId, language ?? string.Empty, segments);
    }

    /// <inheritdoc />
    public async Task<Transcript> ParseFileAsync(
        string path,
        string sourceId,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Parse(content, sourceId, language);
    }

    private static List<(TimeSpan Start, TimeSpan End, string Text)> ParseCues(string[] lines)
    {
        var cues = new List<(TimeSpan, TimeSpan, string)>();
        var i = 0;

        // Skip the WEBVTT header block (WEBVTT line + optional metadata until blank line).
        if (lines.Length > 0 && lines[0].StartsWith("WEBVTT", StringComparison.Ordinal))
        {
            i = 1;
            SkipUntilBlank(lines, ref i);
        }

        while (i < lines.Length)
        {
            SkipBlankLines(lines, ref i);
            if (i >= lines.Length) break;

            if (IsCommentBlock(lines[i]))
            {
                SkipUntilBlank(lines, ref i);
                continue;
            }

            if (TryParseCue(lines, ref i, out var cue))
                cues.Add(cue);
        }

        return cues;
    }

    /// <summary>
    /// Reads one cue (optional identifier, timing line, text lines). Returns false for
    /// a malformed block and leaves the index at the next blank line.
    /// </summary>
    private static bool TryParseCue(
        string[] lines,
        ref int i,
        out (TimeSpan Start, TimeSpan End, string Text) cue)
    {
        cue = default;

        // Cue identifier line is optional; the timing line always contains "-->".
        var timingLine = lines[i];
        var textStart = i + 1;
        if (!timingLine.Contains("-->", StringComparison.Ordinal) && i + 1 < lines.Length)
        {
            timingLine = lines[i + 1];
            textStart = i + 2;
        }

        if (!timingLine.Contains("-->", StringComparison.Ordinal))
        {
            SkipUntilBlank(lines, ref i);
            return false;
        }

        var timing = ParseTimingLine(timingLine);
        if (timing is null)
        {
            SkipUntilBlank(lines, ref i);
            return false;
        }

        i = textStart;
        var textLines = new List<string>();
        while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
        {
            textLines.Add(lines[i]);
            i++;
        }

        cue = (timing.Value.Start, timing.Value.End, string.Join("\n", textLines));
        return true;
    }

    private static void SkipBlankLines(string[] lines, ref int i)
    {
        while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
            i++;
    }

    private static void SkipUntilBlank(string[] lines, ref int i)
    {
        while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
            i++;
    }

    private static bool IsCommentBlock(string line) =>
        line.StartsWith("NOTE", StringComparison.Ordinal)
        || line.StartsWith("STYLE", StringComparison.Ordinal)
        || line.StartsWith("REGION", StringComparison.Ordinal);

    private static (TimeSpan Start, TimeSpan End)? ParseTimingLine(string line)
    {
        var arrowIdx = line.IndexOf("-->", StringComparison.Ordinal);
        if (arrowIdx < 0) return null;

        var startStr = line[..arrowIdx].Trim();
        var endPart = line[(arrowIdx + 3)..].Trim();

        // Cue settings after the end timestamp are separated by whitespace.
        var spaceIdx = endPart.IndexOf(' ');
        if (spaceIdx >= 0) endPart = endPart[..spaceIdx];

        var start = ParseTimestamp(startStr);
        var end = ParseTimestamp(endPart);
        if (start is null || end is null) return null;
        return (start.Value, end.Value);
    }

    private static TimeSpan? ParseTimestamp(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Replace(',', '.'); // SRT compatibility
        var parts = s.Split(':');

        if (parts.Length == 3
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var h)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var m)
            && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var sec))
        {
            return TimeSpan.FromHours(h) + TimeSpan.FromMinutes(m) + TimeSpan.FromSeconds(sec);
        }

        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var m2)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var sec2))
        {
            return TimeSpan.FromMinutes(m2) + TimeSpan.FromSeconds(sec2);
        }

        return null;
    }

    private static List<SubtitleSegment> ProcessCues(List<(TimeSpan Start, TimeSpan End, string Text)> cues)
    {
        var segments = new List<SubtitleSegment>();
        var prevLines = new List<string>();
        var nextMarker = MarkerInterval;

        foreach (var cue in cues)
        {
            if ((cue.End - cue.Start).TotalMilliseconds < MinCueDurationMs)
                continue;

            var cleaned = CleanCueText(cue.Text);
            var currLines = cleaned
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            if (currLines.Count == 0) continue;

            var overlap = FindSuffixPrefixOverlap(prevLines, currLines);
            var newLines = currLines.Skip(overlap).ToList();
            if (newLines.Count == 0) continue;

            while (cue.Start >= nextMarker)
            {
                segments.Add(new SubtitleSegment(
                    nextMarker, nextMarker, FormatMarker(nextMarker), IsTimestampMarker: true));
                nextMarker += MarkerInterval;
            }

            foreach (var line in newLines)
                segments.Add(new SubtitleSegment(cue.Start, cue.End, line));

            prevLines = currLines;
        }

        return segments;
    }

    /// <summary>
    /// Returns the largest k such that currLines[0..k) equals prevLines[^k..].
    /// This is the rolling-window heuristic: YouTube cues extend the tail of the previous cue,
    /// so the non-overlapping suffix of the current cue is the genuinely new text.
    /// </summary>
    private static int FindSuffixPrefixOverlap(List<string> prevLines, List<string> currLines)
    {
        var max = Math.Min(prevLines.Count, currLines.Count);
        for (var k = max; k > 0; k--)
        {
            var match = true;
            for (var j = 0; j < k; j++)
            {
                if (!string.Equals(prevLines[prevLines.Count - k + j], currLines[j], StringComparison.Ordinal))
                {
                    match = false;
                    break;
                }
            }
            if (match) return k;
        }
        return 0;
    }

    private static string CleanCueText(string text)
    {
        // Strip first (structural VTT tags), decode second (so user-typed &lt;tag&gt; survives).
        var stripped = VttTagRegex().Replace(text, string.Empty);
        return WebUtility.HtmlDecode(stripped);
    }

    private static string FormatMarker(TimeSpan ts)
    {
        var h = (int)ts.TotalHours;
        return $"[{h:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}]";
    }
}
