namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class WorkspacePaneEmptyStateContractTests
{
    [Fact]
    public void WorkspacePanes_ShouldUseCardedEmptyAndLoadingStates()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pageXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "BundleExplorerPage.xaml"));
        var assetPaneXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "AssetPaneView.xaml"));
        var inspectorXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "InspectorPanelView.xaml"));
        var assetPaneViewModel = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "ViewModels",
            "BundleExplorer",
            "Bindings",
            "AssetPaneViewModel.cs"));
        var assetPaneListViewModel = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "ViewModels",
            "BundleExplorer",
            "Bindings",
            "AssetPaneViewModel.Lists.cs"));

        Assert.Contains("x:Name=\"BundlePaneEmptyStateCard\"", pageXaml);
        Assert.Contains("x:Name=\"BundleSearchEmptyStateCard\"", pageXaml);
        Assert.Contains("x:Name=\"AssetPaneEmptyStateCard\"", assetPaneXaml);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.EmptyStateLoadingVisibility, Mode=OneWay}\"", assetPaneXaml);
        Assert.Contains("Glyph=\"{x:Bind ViewModel.EmptyStateIconGlyph, Mode=OneWay}\"", assetPaneXaml);
        Assert.Contains("x:Name=\"InspectorEmptyStateCard\"", inspectorXaml);
        Assert.Contains("public bool IsListLoading", assetPaneViewModel);
        Assert.Contains("IsListLoading = true;", assetPaneListViewModel);
        Assert.Contains("IsListLoading = false;", assetPaneListViewModel);
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
