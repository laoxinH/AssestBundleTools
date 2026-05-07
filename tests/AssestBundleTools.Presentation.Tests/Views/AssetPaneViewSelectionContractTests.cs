namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class AssetPaneViewSelectionContractTests
{
    [Fact]
    public void AssetPaneListViews_ShouldLetSelectionCoordinatorOwnSelectionWriteBack()
    {
        var xaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "AssetPaneView.xaml"));

        Assert.Contains("SelectionChanged=\"OnAssetListSelectionChanged\"", xaml);
        Assert.Contains("SelectionChanged=\"OnSpineGroupListSelectionChanged\"", xaml);
        Assert.DoesNotContain("SelectedItem=\"{x:Bind ViewModel.SelectedAsset, Mode=TwoWay}\"", xaml);
        Assert.DoesNotContain("SelectedItem=\"{x:Bind ViewModel.SelectedSpineGroup, Mode=TwoWay}\"", xaml);
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
