namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class BundleExplorerToolbarContractTests
{
    [Fact]
    public void WorkspaceToolbar_ShouldUseBorderlessIconsAndHiddenHorizontalScrollbar()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pageXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "BundleExplorerPage.xaml"));

        Assert.Contains("x:Key=\"WorkspaceChipIconBadgeStyle\"", pageXaml);
        Assert.Contains("<Setter Property=\"Background\" Value=\"Transparent\" />", pageXaml);
        Assert.Contains("<Setter Property=\"BorderBrush\" Value=\"Transparent\" />", pageXaml);
        Assert.Contains("<Setter Property=\"BorderThickness\" Value=\"0\" />", pageXaml);
        Assert.Contains("HorizontalScrollBarVisibility=\"Hidden\"", pageXaml);
    }

    [Fact]
    public void WorkspaceStatusChips_ShouldMatchToolbarChipHeightAndCenterContent()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pageXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "BundleExplorerPage.xaml"));

        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.WorkspaceShell.WorkspaceStatusChips, Mode=OneWay}\"", pageXaml);
        Assert.Contains("MinHeight=\"44\"", pageXaml);
        Assert.Contains("Height=\"44\"", pageXaml);
        Assert.Contains("Padding=\"10,0\"", pageXaml);
        Assert.Contains("VerticalAlignment=\"Center\"", pageXaml);
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
