namespace Beesum.Core.Models;

/// <summary>
/// A single subtitle cue, or a synthetic timestamp marker inserted by the parser.
/// </summary>
/// <param name="Start">Cue start time.</param>
/// <param name="End">Cue end time. For timestamp markers equals <paramref name="Start"/>.</param>
/// <param name="Text">Cue text, or marker text such as <c>[00:15:00]</c>.</param>
/// <param name="IsTimestampMarker">Whether this segment is a synthetic timestamp marker.</param>
public sealed record SubtitleSegment(
    TimeSpan Start,
    TimeSpan End,
    string Text,
    bool IsTimestampMarker = false);
