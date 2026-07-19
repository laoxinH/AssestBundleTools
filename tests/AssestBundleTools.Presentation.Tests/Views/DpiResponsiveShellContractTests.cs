namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class DpiResponsiveShellContractTests
{
    [Fact]
    public void MainWindow_ShouldUseDpiAwarePhysicalLayout()
    {
        var source = ReadPresentationFile("MainWindow.xaml.cs");

        Assert.Contains("DpiAwareWindowLayoutService.Apply(", source);
        Assert.Contains("DpiAwareWindowLayoutProfile.MainWindow", source);
    }

    [Fact]
    public void Shell_ShouldCollapseInlineNavigationAtNarrowEffectiveWidths()
    {
        var source = ReadPresentationFile("Views", "ShellPage.xaml.cs");

        Assert.Contains("CompactNavigationWidthThreshold = 1360d", source);
        Assert.Contains("ApplyResponsiveNavigationLayout(e.NewSize.Width)", source);
        Assert.Contains("RootNavigationView.IsPaneOpen = false", source);
        Assert.DoesNotContain("RootNavigationView.IsPaneOpen = true", source);
    }

    private static string ReadPresentationFile(params string[] relativePath)
    {
        var pathParts = new[]
        {
            FindRepositoryRoot(),
            "src",
            "AssestBundleTools.Presentation.WinUI"
        }.Concat(relativePath).ToArray();

        return File.ReadAllText(Path.Combine(pathParts));
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
