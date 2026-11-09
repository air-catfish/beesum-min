using System.Globalization;
using Beesum.Core.Localization;
using Xunit;

namespace Beesum.Core.Tests.Localization;

public class AppLanguageTests
{
    [Theory]
    [InlineData("ru", "ru-RU")]
    [InlineData("ru", "ru")]
    [InlineData("en", "en-US")]
    [InlineData("en", "de-DE")]
    [InlineData("en", "")]
    public void Resolve_WithoutOverride_UsesSupportedUiCulture(string expected, string uiCulture)
    {
        var culture = uiCulture.Length == 0
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(uiCulture);

        Assert.Equal(expected, AppLanguage.Resolve(null, culture));
    }

    [Theory]
    [InlineData("ru", "ru")]
    [InlineData("en", "en")]
    [InlineData("ru", "RU")]
    [InlineData("en", "klingon")]
    public void Resolve_WithOverride_ForcesSupportedLanguage(string expected, string forced)
    {
        Assert.Equal(expected, AppLanguage.Resolve(forced, CultureInfo.GetCultureInfo("en-US")));
    }
}
