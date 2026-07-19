namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class LogPanelPresentationContractTests
{
    [Fact]
    public void LogPanel_ShouldExposeFilteringAndEntryAndWholeLogCopyCommands()
    {
        var repositoryRoot = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "Shared",
            "LogPanel.xaml"));
        var code = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "Shared",
            "LogPanel.xaml.cs"));

        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.FilteredEntries, Mode=OneWay}\"", xaml);
        Assert.Contains("Style=\"{StaticResource CompactSegmentRadioButtonStyle}\"", xaml);
        Assert.Contains("Click=\"OnCopyEntryButtonClick\"", xaml);
        Assert.Contains("Click=\"OnCopyAllButtonClick\"", xaml);
        Assert.Contains("IsTextSelectionEnabled=\"True\"", xaml);
        Assert.Contains("SystemFillColorCautionBrush", xaml);
        Assert.Contains("SystemFillColorCriticalBrush", xaml);
        Assert.Contains("Clipboard.SetContent(dataPackage);", code);
        Assert.DoesNotContain("IsEnabled=\"False\"", xaml);
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
