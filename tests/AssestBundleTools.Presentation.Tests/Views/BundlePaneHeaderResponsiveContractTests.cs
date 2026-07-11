namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class BundlePaneHeaderResponsiveContractTests
{
    [Fact]
    public void BundlePaneHeader_ShouldMoveActionsToSecondRow_WhenPaneIsNarrow()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagePath = Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "BundleExplorerPage");
        var pageXaml = File.ReadAllText(pagePath + ".xaml");
        var pageCode = File.ReadAllText(pagePath + ".xaml.cs");

        Assert.Contains("x:Name=\"BundlePaneHeaderGrid\"", pageXaml);
        Assert.Contains("SizeChanged=\"OnBundlePaneHeaderGridSizeChanged\"", pageXaml);
        Assert.Contains("x:Name=\"BundlePaneHeaderActions\"", pageXaml);
        Assert.Contains("TextWrapping=\"NoWrap\"", pageXaml);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", pageXaml);
        Assert.Contains("BundlePaneHeaderTwoRowThreshold = 268d", pageCode);
        Assert.Contains("Grid.SetRow(BundlePaneHeaderActions, useTwoRows ? 1 : 0);", pageCode);
        Assert.Contains("Grid.SetColumn(BundlePaneHeaderActions, useTwoRows ? 0 : 1);", pageCode);
        Assert.Contains("Grid.SetColumnSpan(LeftPanelTitleText, useTwoRows ? 2 : 1);", pageCode);
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
