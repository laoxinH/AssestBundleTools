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

    [Fact]
    public void AssetPaneViewModeToggles_ShouldUseDedicatedViewModeChangedEvent()
    {
        var xaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "BundleExplorerPage.xaml"));

        Assert.Contains("ViewModeChanged=\"OnPrimaryPaneViewModeChanged\"", xaml);
        Assert.Contains("FilterChanged=\"OnPrimaryPaneFilterChanged\"", xaml);
    }

    [Fact]
    public void BundleExplorerPage_ShouldExposeActiveDocumentLocateCommand()
    {
        var xaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "BundleExplorerPage.xaml"));

        Assert.Contains("x:Name=\"PrimaryAssetPaneView\"", xaml);
        Assert.Contains("x:Name=\"LocateActiveDocumentButton\"", xaml);
        Assert.Contains("Click=\"OnLocateActiveDocumentButtonClick\"", xaml);
    }

    [Fact]
    public void AssetPaneRows_ShouldExposeContextMenuCommands()
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

        Assert.Contains("OpenAssetPaneItemInNewWindowMenuItem", xaml);
        Assert.Contains("ExtractAssetPaneItemMenuItem", xaml);
        Assert.Contains("ReplaceAssetPaneItemMenuItem", xaml);
        Assert.Contains("EnterAssetPaneItemMultiSelectionMenuItem", xaml);
        Assert.Contains("Opening=\"OnAssetRowContextFlyoutOpening\"", xaml);
        Assert.Contains("Opening=\"OnSpineGroupRowContextFlyoutOpening\"", xaml);
        Assert.Contains("Click=\"OnOpenAssetPaneItemInNewWindowMenuItemClick\"", xaml);
        Assert.Contains("Click=\"OnExtractAssetPaneItemMenuItemClick\"", xaml);
        Assert.Contains("Click=\"OnReplaceAssetPaneItemMenuItemClick\"", xaml);
        Assert.Contains("Click=\"OnEnterAssetPaneItemMultiSelectionMenuItemClick\"", xaml);
        Assert.Contains("x:Name=\"SpineGroupListView\"", xaml);
        Assert.Contains("SelectionMode=\"Extended\"", xaml);
    }

    [Fact]
    public void BundleExplorerPage_ShouldWireAssetPaneContextMenuRequests()
    {
        var xaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "BundleExplorerPage.xaml"));

        Assert.Contains(
            "OpenSelectedItemInNewWindowRequested=\"OnPrimaryPaneOpenSelectedItemInNewWindowRequested\"",
            xaml);
        Assert.Contains(
            "ExtractSelectedItemRequested=\"OnPrimaryPaneExtractSelectedItemRequested\"",
            xaml);
        Assert.Contains(
            "ReplaceSelectedAssetRequested=\"OnPrimaryPaneReplaceSelectedAssetRequested\"",
            xaml);
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
