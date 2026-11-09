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

    private static string FormatTimestamp(TimeSpan value)
    {
        var totalHours = (int)value.TotalHours;
        return $"{totalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
    }
}
