using System.Globalization;

namespace Beesum.Core.Localization;

/// <summary>
/// Chooses the UI language. The default is English; the system UI language is used
/// when it is supported, and the <c>BEESUM_LANG</c> environment variable overrides
/// both (useful for Docker and scripted runs).
/// </summary>
public static class AppLanguage
{
    private const string English = "en";
    private const string Russian = "ru";

    /// <summary>Current two-letter UI language code: <c>en</c> or <c>ru</c>.</summary>
    public static string Current => Resolve(
        Environment.GetEnvironmentVariable("BEESUM_LANG"),
        CultureInfo.CurrentUICulture);

    /// <summary>Resolves a supported language code; unsupported values fall back to English.</summary>
    public static string Resolve(string? forcedLanguage, CultureInfo uiCulture)
    {
        ArgumentNullException.ThrowIfNull(uiCulture);

        if (!string.IsNullOrWhiteSpace(forcedLanguage))
        {
            var forced = forcedLanguage.Trim();
            if (forced.StartsWith(Russian, StringComparison.OrdinalIgnoreCase)) return Russian;
            if (forced.StartsWith(English, StringComparison.OrdinalIgnoreCase)) return English;
            return English;
        }

        return uiCulture.TwoLetterISOLanguageName.Equals(Russian, StringComparison.OrdinalIgnoreCase)
            ? Russian
            : English;
    }
}
