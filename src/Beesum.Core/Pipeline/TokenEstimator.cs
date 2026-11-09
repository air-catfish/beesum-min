using Beesum.Core.Models;

namespace Beesum.Core.Pipeline;

/// <summary>
/// Rough token estimate. Latin text is near four characters per token; Cyrillic
/// and CJK are closer to two or three. Dividing by three over-estimates Latin,
/// which is the safer direction for context limits.
/// </summary>
internal static class TokenEstimator
{
    public static int Estimate(string? text) =>
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 2) / 3;

    public static int Estimate(Transcript transcript) =>
        Estimate(transcript.ToPlainText());
}
