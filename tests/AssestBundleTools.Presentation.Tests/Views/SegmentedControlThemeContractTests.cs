namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class SegmentedControlThemeContractTests
{
    [Fact]
    public void SegmentedControls_ShouldUseSystemAccentForSelectedState()
    {
        var appXaml = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "App.xaml"));
        var radioStyle = ReadStyle(appXaml, "CompactSegmentRadioButtonStyle", "CompactSegmentToggleButtonStyle");
        var toggleStyleStart = appXaml.IndexOf(
            "x:Key=\"CompactSegmentToggleButtonStyle\"",
            StringComparison.Ordinal);
        Assert.True(toggleStyleStart >= 0);
        var toggleStyle = appXaml[toggleStyleStart..];

        Assert.Contains("Background=\"{ThemeResource AccentFillColorDefaultBrush}\"", radioStyle);
        Assert.Contains("BorderBrush=\"{ThemeResource AccentFillColorDefaultBrush}\"", radioStyle);
        Assert.Contains("TextOnAccentFillColorPrimaryBrush", radioStyle);
        Assert.Contains("Background=\"{ThemeResource AccentFillColorDefaultBrush}\"", toggleStyle);
        Assert.Contains("BorderBrush=\"{ThemeResource AccentFillColorDefaultBrush}\"", toggleStyle);
        Assert.Contains("TextOnAccentFillColorPrimaryBrush", toggleStyle);
        Assert.DoesNotContain("SegmentedSelectedBackgroundBrush", appXaml);
        Assert.DoesNotContain("SegmentedSelectedBorderBrush", appXaml);
        Assert.DoesNotContain("#FF3B3B3D", appXaml);
    }

    private static string ReadStyle(string xaml, string styleKey, string nextStyleKey)
    {
        var start = xaml.IndexOf($"x:Key=\"{styleKey}\"", StringComparison.Ordinal);
        var end = xaml.IndexOf($"x:Key=\"{nextStyleKey}\"", start, StringComparison.Ordinal);
        Assert.True(start >= 0);
        Assert.True(end > start);
        return xaml[start..end];
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
