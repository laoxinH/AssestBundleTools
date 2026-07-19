namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class WorkspacePaneEmptyStateContractTests
{
    [Fact]
    public void WorkspacePanes_ShouldUseBorderlessEmptyAndLoadingStates()
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

        Assert.Contains("x:Name=\"BundlePaneEmptyState\"", pageXaml);
        Assert.Contains("x:Name=\"BundlePaneEmptyStateTitleText\"", pageXaml);
        Assert.Contains("Text=\"{x:Bind ViewModel.BundlePane.BundlePaneEmptyStateTitleText, Mode=OneWay}\"", pageXaml);
        Assert.Contains("x:Name=\"BundleSearchEmptyState\"", pageXaml);
        Assert.Contains("x:Name=\"BundleSearchEmptyStateTitleText\"", pageXaml);
        Assert.Contains("Style=\"{StaticResource WorkbenchEmptyStateLayoutStyle}\"", pageXaml);
        Assert.DoesNotContain("WorkbenchEmptyStateCardStyle", pageXaml);
        Assert.Contains("x:Name=\"AssetPaneEmptyState\"", assetPaneXaml);
        Assert.Contains("Style=\"{StaticResource AssetPaneEmptyStateLayoutStyle}\"", assetPaneXaml);
        Assert.DoesNotContain("AssetPaneEmptyStateCardStyle", assetPaneXaml);
        Assert.Contains("Text=\"{x:Bind ViewModel.EmptyStateTitleText, Mode=OneWay}\"", assetPaneXaml);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.EmptyStateLoadingVisibility, Mode=OneWay}\"", assetPaneXaml);
        Assert.Contains("Glyph=\"{x:Bind ViewModel.EmptyStateIconGlyph, Mode=OneWay}\"", assetPaneXaml);
        Assert.Contains("x:Name=\"InspectorEmptyState\"", inspectorXaml);
        Assert.Contains("Style=\"{StaticResource InspectorEmptyStateLayoutStyle}\"", inspectorXaml);
        Assert.DoesNotContain("InspectorEmptyStateCardStyle", inspectorXaml);
        Assert.Contains("public bool IsListLoading", assetPaneViewModel);
        Assert.Contains("public string EmptyStateTitleText", assetPaneViewModel);
        Assert.Contains("IsListLoading = true;", assetPaneListViewModel);
        Assert.Contains("IsListLoading = false;", assetPaneListViewModel);
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("ASSESTBUNDLETOOLS_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            var configuredRepositoryRoot = TryFindRepositoryRoot(new DirectoryInfo(configuredRoot));
            if (configuredRepositoryRoot is not null)
            {
                return configuredRepositoryRoot;
            }
        }

        var repositoryRoot = TryFindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory));
        if (repositoryRoot is not null)
        {
            return repositoryRoot;
        }

        repositoryRoot = TryFindRepositoryRoot(new DirectoryInfo(Environment.CurrentDirectory));
        if (repositoryRoot is not null)
        {
            return repositoryRoot;
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }

    private static string? TryFindRepositoryRoot(DirectoryInfo? directory)
    {
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "AssestBundleTools.Presentation.WinUI")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests", "AssestBundleTools.Presentation.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
