namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class AppThemeWindowContractTests
{
    [Fact]
    public void ThemeCoordinator_ShouldUpdateAllWindowRootsAndCaptionButtons()
    {
        var repositoryRoot = FindRepositoryRoot();
        var themeService = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Services",
            "Preferences",
            "AppThemeService.cs"));
        var windowAppearance = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Services",
            "Platform",
            "Windowing",
            "ModernWindowAppearanceService.cs"));

        Assert.Contains("rootElement.RequestedTheme", themeService);
        Assert.Contains("ActualThemeChanged += OnRootActualThemeChanged", themeService);
        Assert.Contains("ApplyTitleBarTheme(window, rootElement.ActualTheme)", themeService);
        Assert.Contains("titleBar.ButtonForegroundColor = foreground", windowAppearance);
        Assert.Contains("titleBar.ButtonInactiveForegroundColor = inactiveForeground", windowAppearance);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "AssestBundleTools.Presentation.WinUI")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests", "AssestBundleTools.Presentation.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
