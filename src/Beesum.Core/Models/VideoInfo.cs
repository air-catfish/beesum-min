using System.Diagnostics.CodeAnalysis;

namespace Beesum.Core.Models;

/// <summary>Metadata about a YouTube video.</summary>
/// <param name="Id">Provider video id.</param>
/// <param name="Title">Video title.</param>
/// <param name="Duration">Video duration.</param>
/// <param name="Author">Channel/author name, if known.</param>
/// <param name="ThumbnailUrl">Thumbnail URL as a plain string, if known.</param>
public sealed record VideoInfo(
    string Id,
    string Title,
    TimeSpan Duration,
    string? Author = null,
    [property: SuppressMessage(
        "Design",
        "CA1056:URI properties should not be strings",
        Justification = "Thumbnail URLs are passed to the UI as plain strings.")]
    string? ThumbnailUrl = null);
