namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class AssetEditorWindowOperationRefreshContractTests
{
    [Fact]
    public void OperationRefresh_ShouldIgnoreTabsDetachedDuringAsyncRefresh()
    {
        var operationRefreshCode = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "ViewModels",
                "BundleExplorer",
                "Bindings",
                "AssetEditorWindowViewModel.OperationRefresh.cs"));

        Assert.Contains("!DocumentGroup.AssetTabs.Contains(assetTab)", operationRefreshCode);
        Assert.Contains("!DocumentGroup.SpineGroupTabs.Contains(spineGroupTab)", operationRefreshCode);
        Assert.Contains("AssetPreviewInvalidationPolicy.HasEffectivePreviewProjectionChanged", operationRefreshCode);
        Assert.Contains("_assetPreviewLoadCoordinator.ReloadForegroundPreview(assetTab);", operationRefreshCode);
        Assert.Contains("_assetPreviewLoadCoordinator.InvalidatePreview(assetTab);", operationRefreshCode);
    }

    [Fact]
    public void SpineEditExit_ShouldReloadOnlyWhenDiscardingUnsavedDraft()
    {
        var actionsCode = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "ViewModels",
                "BundleExplorer",
                "Bindings",
                "AssetEditorWindowViewModel.Actions.cs"));

        Assert.Contains("var shouldReloadPreview = spineGroupTab.ExitSpineEditMode(discardChanges);", actionsCode);
        Assert.Contains("if (shouldReloadPreview)", actionsCode);
        Assert.Contains("await ReloadSpineGroupDetailsAsync(spineGroupTab);", actionsCode);

        var applyMethod = actionsCode[
            actionsCode.IndexOf("public async Task<bool> ApplySpineEditAsync()", StringComparison.Ordinal)..actionsCode.IndexOf(
                "public async Task ExitSpineEditModeForActiveGroupAsync",
                StringComparison.Ordinal)];
        Assert.DoesNotContain("ReloadSpineGroupDetailsAsync", applyMethod);
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
