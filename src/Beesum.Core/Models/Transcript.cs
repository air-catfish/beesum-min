namespace Beesum.Core.Models;

/// <summary>
/// A parsed subtitle transcript composed of time-stamped segments.
/// </summary>
/// <param name="SourceId">Stable source identifier (e.g. YouTube video id).</param>
/// <param name="Language">BCP-47 language tag, or an empty string when unknown.</param>
/// <param name="Segments">Ordered segments, including synthetic timestamp markers.</param>
public sealed record Transcript(
    string SourceId,
    string Language,
    IReadOnlyList<SubtitleSegment> Segments)
{
    /// <summary>
    /// All segment texts, one per segment, including timestamp-marker lines.
    /// </summary>
    public IReadOnlyList<string> TextLines => Segments.Select(s => s.Text).ToList();

    /// <summary>
    /// Joins <see cref="TextLines"/> with <c>\n</c> into a single plain-text block.
    /// </summary>
    public string ToPlainText() => string.Join("\n", TextLines);

    /// <summary>
    /// Joins cue texts with <c>\n</c>. When <paramref name="timestamps"/> is true,
    /// each cue is prefixed with its start time as <c>[hh:mm:ss]</c> and synthetic
    /// 15-minute marker segments are omitted.
    /// </summary>
    public string ToPlainText(bool timestamps)
    {
        if (!timestamps) return ToPlainText();

        return string.Join(
            "\n",
            Segments
                .Where(s => !s.IsTimestampMarker && !string.IsNullOrWhiteSpace(s.Text))
                .Select(s => $"[{FormatTimestamp(s.Start)}] {s.Text}"));
    }

    /// <summary>
    /// Returns a transcript restricted to cues overlapping <paramref name="from"/>..<paramref name="to"/>.
    /// Null bounds are open. Markers inside the range are kept.
    /// </summary>
    public Transcript Slice(TimeSpan? from, TimeSpan? to)
    {
        if (from is null && to is null) return this;
        if (from is not null && to is not null && from.Value >= to.Value)
            throw new ArgumentException("'from' must be before 'to'.", nameof(from));

        var segments = Segments
            .Where(s => (from is null || s.End > from.Value) &&
                        (to is null || s.Start < to.Value))
            .ToList();

        return this with { Segments = segments };
    }

    private static string FormatTimestamp(TimeSpan value)
    {
        var totalHours = (int)value.TotalHours;
        return $"{totalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
    }
}
