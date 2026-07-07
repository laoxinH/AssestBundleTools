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

    [Fact]
    public void TextEditActionBar_ShouldRefreshWhenDraftOrBusyStateChanges()
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
        var viewModelCode = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "ViewModels",
                "BundleExplorer",
                "Bindings",
                "AssetEditorWindowViewModel.cs"));

        var requestMethod = actionsCode[
            actionsCode.IndexOf("public async Task<bool> RequestTextEditModeForActiveAssetAsync()", StringComparison.Ordinal)..actionsCode.IndexOf(
                "public async Task<bool> ApplyTextEditAsync()",
                StringComparison.Ordinal)];
        var applyMethod = actionsCode[
            actionsCode.IndexOf("public async Task<bool> ApplyTextEditAsync()", StringComparison.Ordinal)..actionsCode.IndexOf(
                "internal bool TryBeginTextEditSaveOperation()",
                StringComparison.Ordinal)];
        var saveOperationMethods = actionsCode[
            actionsCode.IndexOf("internal bool TryBeginTextEditSaveOperation()", StringComparison.Ordinal)..actionsCode.IndexOf(
                "public async Task<bool> ExitTextEditModeForActiveAssetAsync()",
                StringComparison.Ordinal)];
        var exitMethod = actionsCode[
            actionsCode.IndexOf("public async Task<bool> ExitTextEditModeForActiveAssetAsync()", StringComparison.Ordinal)..actionsCode.IndexOf(
                "public async Task ExitSpineEditModeForActiveGroupAsync",
                StringComparison.Ordinal)];
        var busySetter = viewModelCode[
            viewModelCode.IndexOf("public bool IsBusy", StringComparison.Ordinal)..viewModelCode.IndexOf(
                "public void Dispose()",
                StringComparison.Ordinal)];

        Assert.Contains("assetTab.StartTextEditMode(draft);", requestMethod);
        Assert.Contains("TryRefreshAssetDocumentDetailsBeforeReopeningTextEditAsync(assetTab)", requestMethod);
        Assert.Contains("ShouldReopenTextEditDraft(assetTab.Details, existingDraft)", requestMethod);
        Assert.Contains("ObserveActiveTextEditDraft();", requestMethod);
        Assert.Contains("DocumentGroup.ActiveAssetTab?.ExitTextEditMode(clearDraftReference: false);", applyMethod);
        Assert.Contains("ObserveActiveTextEditDraft();", applyMethod);
        Assert.Contains("IsTextEditLoading", applyMethod);
        Assert.Contains("IsBusy", applyMethod);
        Assert.Contains("SetTextEditSaving(true);", saveOperationMethods);
        Assert.Contains("SetTextEditSaving(false);", saveOperationMethods);
        Assert.Contains("assetTab?.ExitTextEditMode(clearDraftReference: true);", exitMethod);
        Assert.Contains("ObserveActiveTextEditDraft();", exitMethod);
        Assert.Contains("NotifyActionBarStates();", busySetter);
    }

    [Fact]
    public void TextEditSaveButton_ShouldShowBusyProgressWhileSaving()
    {
        var repositoryRoot = FindRepositoryRoot();
        var actionBarCode = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "ViewModels",
                "BundleExplorer",
                "Bindings",
                "AssetEditorWindowViewModel.ActionBars.cs"));
        var xaml = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "AssetEditorWindow.xaml"));

        Assert.Contains("public bool IsTextEditSaving", actionBarCode);
        Assert.Contains("public bool IsTextEditBusy => IsTextEditLoading || IsTextEditSaving;", actionBarCode);
        Assert.Contains("ResourceKeys.TextAssetEditorSavingEditButton", actionBarCode);
        Assert.Contains("ResourceKeys.TextAssetEditorSavingEditButtonToolTip", actionBarCode);
        Assert.Contains("!IsTextEditBusy", actionBarCode);
        Assert.Contains("IsActive=\"{x:Bind ViewModel.IsTextEditBusy, Mode=OneWay}\"", xaml);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.TextEditBusyVisibility, Mode=OneWay}\"", xaml);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.SaveTextEditIconVisibility, Mode=OneWay}\"", xaml);
    }

    [Fact]
    public void TextEditExit_ShouldNotDiscardAppliedBaseline_WhenClean()
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

        var exitMethod = actionsCode[
            actionsCode.IndexOf("public async Task<bool> ExitTextEditModeForActiveAssetAsync()", StringComparison.Ordinal)..actionsCode.IndexOf(
                "public async Task ExitSpineEditModeForActiveGroupAsync",
                StringComparison.Ordinal)];

        Assert.Contains("draft.IsAppliedBaseline", exitMethod);
        Assert.Contains("assetTab?.ExitTextEditMode(clearDraftReference: false);", exitMethod);
        Assert.Contains("DiscardTextAssetEditDraftAsync", exitMethod);
    }

    [Fact]
    public void TextEditCoordinator_ShouldPromoteDraftBaseline_AfterPayloadUpdate()
    {
        var coordinatorCode = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "ViewModels",
                "BundleExplorer",
                "Coordinators",
                "AssetEditorWindowTextEditActionCoordinator.cs"));

        var applyMethod = coordinatorCode[
            coordinatorCode.IndexOf("public async Task<bool> ApplyTextAssetEditDraftAsync()", StringComparison.Ordinal)..coordinatorCode.IndexOf(
                "public async Task<bool> DiscardTextAssetEditDraftAsync()",
                StringComparison.Ordinal)];
        var markSavedIndex = applyMethod.IndexOf("draft.MarkSaved(updateResult.Value?.Reference, updateResult.Value?.PatchBaseline);", StringComparison.Ordinal);
        var applyCommandIndex = applyMethod.IndexOf("var command = new ApplyTextAssetEditDraftCommand", StringComparison.Ordinal);

        Assert.Contains("TextContent: null", applyMethod);
        Assert.Contains("var rangeEdits = draft.CreateSaveRangeEdits();", applyMethod);
        Assert.Contains("var patchBaseline = draft.CreateSavePatchBaseline();", applyMethod);
        Assert.Contains("RangeEdits: rangeEdits", applyMethod);
        Assert.Contains("PatchBaseline: patchBaseline", applyMethod);
        Assert.True(markSavedIndex >= 0, "Text draft baseline should be promoted after payload update.");
        Assert.True(applyCommandIndex >= 0, "Text draft apply command should still be issued.");
        Assert.True(markSavedIndex < applyCommandIndex, "Draft baseline should be promoted before apply so retry uses the latest draft payload.");
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
