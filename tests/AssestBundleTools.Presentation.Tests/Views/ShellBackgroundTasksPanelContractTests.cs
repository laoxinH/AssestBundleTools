namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class ShellBackgroundTasksPanelContractTests
{
    [Fact]
    public void BackgroundTaskSummary_ShouldUseStableOverlayPanelInsteadOfTooltipFlyoutHover()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "ShellPage.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "ShellPage.xaml.cs"));

        var summaryBlock = xaml[
            xaml.IndexOf("x:Name=\"BackgroundTaskSummaryRegion\"", StringComparison.Ordinal)..
            xaml.IndexOf("x:Name=\"BackgroundTasksPanelHost\"", StringComparison.Ordinal)];

        Assert.Contains("x:Name=\"BackgroundTasksPanelHost\"", xaml);
        Assert.Contains("PointerEntered=\"OnBackgroundTasksPanelPointerEntered\"", xaml);
        Assert.Contains("PointerExited=\"OnBackgroundTasksPanelPointerExited\"", xaml);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding TaskSummaryToolTipText}\"", summaryBlock);
        Assert.DoesNotContain("ToolTipService.ToolTip=\"{Binding TaskSummaryToolTipText}\"", summaryBlock);
        Assert.DoesNotContain("FlyoutBase.AttachedFlyout", summaryBlock);
        Assert.Contains("BackgroundTasksPanelHost.Visibility = Visibility.Visible;", codeBehind);
        Assert.Contains("BackgroundTasksPanelHost.Visibility = Visibility.Collapsed;", codeBehind);
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
