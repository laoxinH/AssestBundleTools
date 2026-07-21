using AssestBundleTools.Presentation.WinUI.Services;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.Tests.Services;

public sealed class AppThemeServiceTests
{
    [Theory]
    [InlineData(AppThemePreference.SystemDefault, ElementTheme.Default)]
    [InlineData(AppThemePreference.Light, ElementTheme.Light)]
    [InlineData(AppThemePreference.Dark, ElementTheme.Dark)]
    public void ToElementTheme_ShouldMapEveryPersistedPreference(
        AppThemePreference preference,
        ElementTheme expectedTheme)
    {
        Assert.Equal(expectedTheme, AppThemeService.ToElementTheme(preference));
    }
}
