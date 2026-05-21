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
    public void AssetPaneSelectionChanged_ShouldSuppressDocumentActivation_DuringMultiSelectionMode()
    {
        var code = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "AssetPaneView.xaml.cs"));

        var assetSelectionHandler = code[
            code.IndexOf("private void OnAssetListSelectionChanged", StringComparison.Ordinal)..
            code.IndexOf("private void OnSpineGroupListSelectionChanged", StringComparison.Ordinal)];
        var spineGroupSelectionHandler = code[
            code.IndexOf("private void OnSpineGroupListSelectionChanged", StringComparison.Ordinal)..
            code.IndexOf("private void OnSearchTextChanged", StringComparison.Ordinal)];

        Assert.True(
            assetSelectionHandler.IndexOf("ViewModel?.IsMultiSelectionMode == true", StringComparison.Ordinal) <
            assetSelectionHandler.IndexOf("AssetSelectionChanged?.Invoke", StringComparison.Ordinal));
        Assert.True(
            spineGroupSelectionHandler.IndexOf("ViewModel?.IsMultiSelectionMode == true", StringComparison.Ordinal) <
            spineGroupSelectionHandler.IndexOf("AssetSelectionChanged?.Invoke", StringComparison.Ordinal));
        Assert.Contains("return;", assetSelectionHandler);
        Assert.Contains("return;", spineGroupSelectionHandler);
    }

    [Fact]
    public void AssetPaneMultiSelectionMode_ShouldSwitchListViewToMultipleSelection()
    {
        var code = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "AssetPaneView.MultiSelection.cs"));

        Assert.Contains("AssetListView.SelectionMode", code);
        Assert.Contains("SpineGroupListView.SelectionMode", code);
        Assert.Contains("ListViewSelectionMode.Multiple", code);
        Assert.Contains("ListViewSelectionMode.Extended", code);
    }

    [Fact]
    public void AssetPaneMultiSelectionMode_ShouldInterceptRowPointerPressForToggle()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(
            Path.Combine(
                root,
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "AssetPaneView.xaml"));
        var rowInteractionCode = File.ReadAllText(
            Path.Combine(
                root,
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "AssetPaneView.RowInteraction.cs"));
        var multiSelectionCode = File.ReadAllText(
            Path.Combine(
                root,
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "AssetPaneView.MultiSelection.cs"));
        var selectionCoordinatorCode = File.ReadAllText(
            Path.Combine(
                root,
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Coordinators",
                "AssetPaneSelectionViewCoordinator.cs"));

        Assert.Contains("PointerPressed=\"OnAssetRowPointerPressed\"", xaml);
        Assert.Contains("TryHandleMultiSelectionRowPointerPressed(sender, e);", rowInteractionCode);
        Assert.Contains("e.Handled = true;", multiSelectionCode);
        Assert.Contains("pointer.Properties.IsRightButtonPressed", multiSelectionCode);
        Assert.Contains("pointer.Properties.IsLeftButtonPressed", multiSelectionCode);
        Assert.Contains("ToggleAssetSelectionInMultiSelectionMode", multiSelectionCode);
        Assert.Contains("ToggleSpineGroupSelectionInMultiSelectionMode", multiSelectionCode);
        Assert.Contains("public bool ToggleAssetSelectionInMultiSelectionMode", selectionCoordinatorCode);
        Assert.Contains("public bool ToggleSpineGroupSelectionInMultiSelectionMode", selectionCoordinatorCode);

        var assetToggleHandler = selectionCoordinatorCode[
            selectionCoordinatorCode.IndexOf(
                "public bool ToggleAssetSelectionInMultiSelectionMode",
                StringComparison.Ordinal)..
            selectionCoordinatorCode.IndexOf(
                "public bool ToggleAssetSelectionRangeInMultiSelectionMode",
                StringComparison.Ordinal)];
        var spineGroupToggleHandler = selectionCoordinatorCode[
            selectionCoordinatorCode.IndexOf(
                "public bool ToggleSpineGroupSelectionInMultiSelectionMode",
                StringComparison.Ordinal)..
            selectionCoordinatorCode.IndexOf(
                "public bool ToggleSpineGroupSelectionRangeInMultiSelectionMode",
                StringComparison.Ordinal)];

        Assert.DoesNotContain("AssetPaneListViewSelectionWriter.ApplySelection", assetToggleHandler);
        Assert.DoesNotContain("ScrollIntoView", assetToggleHandler);
        Assert.DoesNotContain("UpdateLayout", assetToggleHandler);
        Assert.DoesNotContain("AssetPaneListViewSelectionWriter.ApplySelection", spineGroupToggleHandler);
        Assert.DoesNotContain("ScrollIntoView", spineGroupToggleHandler);
        Assert.DoesNotContain("UpdateLayout", spineGroupToggleHandler);
    }

    [Fact]
    public void AssetPaneMultiSelectionMode_ShouldSupportPointerDragSelection()
    {
        var root = FindRepositoryRoot();
        var rowInteractionCode = File.ReadAllText(
            Path.Combine(
                root,
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "AssetPaneView.RowInteraction.cs"));
        var multiSelectionCode = File.ReadAllText(
            Path.Combine(
                root,
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "AssetPaneView.MultiSelection.cs"));
        var viewCode = File.ReadAllText(
            Path.Combine(
                root,
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "AssetPaneView.xaml.cs"));
        var selectionCoordinatorCode = File.ReadAllText(
            Path.Combine(
                root,
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Coordinators",
                "AssetPaneSelectionViewCoordinator.cs"));
        var selectionRangeResolverCode = File.ReadAllText(
            Path.Combine(
                root,
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Coordinators",
                "AssetPaneSelectionRangeResolver.cs"));

        Assert.Contains("TryHandleMultiSelectionRowPointerEntered(sender, e);", rowInteractionCode);
        Assert.Contains("EndMultiSelectionDragGesture();", rowInteractionCode);
        Assert.Contains("_isMultiSelectionDragGestureActive", multiSelectionCode);
        Assert.DoesNotContain("_multiSelectionDragShouldSelect", multiSelectionCode);
        Assert.Contains("_lastMultiSelectionDragRowContext", multiSelectionCode);
        Assert.Contains("AddHandler", viewCode);
        Assert.Contains("PointerWheelChangedEvent", viewCode);
        Assert.Contains("handledEventsToo: true", viewCode);
        Assert.Contains("OnAssetPaneListPointerWheelChanged", multiSelectionCode);
        Assert.Contains("DispatcherQueuePriority.Low", multiSelectionCode);
        Assert.Contains("FindRowContextAtPointerPosition", multiSelectionCode);
        Assert.Contains("TryContinueAssetMultiSelectionDrag", multiSelectionCode);
        Assert.Contains("TryContinueSpineGroupMultiSelectionDrag", multiSelectionCode);
        Assert.Contains("ToggleAssetSelectionRangeInMultiSelectionMode", multiSelectionCode);
        Assert.Contains("ToggleSpineGroupSelectionRangeInMultiSelectionMode", multiSelectionCode);
        Assert.Contains("public bool ToggleAssetSelectionRangeInMultiSelectionMode", selectionCoordinatorCode);
        Assert.Contains("public bool ToggleSpineGroupSelectionRangeInMultiSelectionMode", selectionCoordinatorCode);
        Assert.Contains("AssetPaneSelectionRangeResolver.Resolve", selectionCoordinatorCode);
        Assert.Contains("AssetPaneSelectionRangeResolver.ResolveToggleRange", selectionCoordinatorCode);
        Assert.Contains("ResolveRange", selectionRangeResolverCode);
        Assert.Contains("IndexOf", selectionRangeResolverCode);
        Assert.DoesNotContain("SetAssetSelectionInMultiSelectionMode", selectionCoordinatorCode);
        Assert.DoesNotContain("SetSpineGroupSelectionInMultiSelectionMode", selectionCoordinatorCode);

        var assetDragSelectionHandler = selectionCoordinatorCode[
            selectionCoordinatorCode.IndexOf(
                "private bool ApplyAssetSelectionToggleRangeInMultiSelectionMode",
                StringComparison.Ordinal)..
            selectionCoordinatorCode.IndexOf(
                "public bool SelectSpineGroupForContextAction",
                StringComparison.Ordinal)];
        var spineGroupDragSelectionHandler = selectionCoordinatorCode[
            selectionCoordinatorCode.IndexOf(
                "private bool ApplySpineGroupSelectionToggleRangeInMultiSelectionMode",
                StringComparison.Ordinal)..
            selectionCoordinatorCode.IndexOf(
                "public void SynchronizeAssetSelectionFromViewModel",
                StringComparison.Ordinal)];

        Assert.DoesNotContain("ScrollIntoView", assetDragSelectionHandler);
        Assert.DoesNotContain("UpdateLayout", assetDragSelectionHandler);
        Assert.DoesNotContain("AssetPaneListViewSelectionWriter.ApplySelection", assetDragSelectionHandler);
        Assert.DoesNotContain("ScrollIntoView", spineGroupDragSelectionHandler);
        Assert.DoesNotContain("UpdateLayout", spineGroupDragSelectionHandler);
        Assert.DoesNotContain("AssetPaneListViewSelectionWriter.ApplySelection", spineGroupDragSelectionHandler);
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
    public void AssetPaneMultiSelectionBar_ShouldExposeIconCommands()
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

        Assert.Contains("x:Name=\"ExitMultiSelectionButton\"", xaml);
        Assert.Contains("Click=\"OnExitMultiSelectionButtonClick\"", xaml);
        Assert.Contains("x:Name=\"SelectAllMultiSelectionButton\"", xaml);
        Assert.Contains("Click=\"OnSelectAllMultiSelectionButtonClick\"", xaml);
        Assert.Contains("x:Name=\"InvertMultiSelectionButton\"", xaml);
        Assert.Contains("Click=\"OnInvertMultiSelectionButtonClick\"", xaml);
        Assert.Contains("x:Name=\"BatchExtractSelectedButton\"", xaml);
        Assert.Contains("Style=\"{StaticResource AssetPaneToolbarIconButtonStyle}\"", xaml);
        Assert.Contains("Text=\"{x:Bind ViewModel.SelectedItemCount, Mode=OneWay}\"", xaml);
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

    [Fact]
    public void BundleExplorerPage_AssetPaneReplaceContextMenu_ShouldUseFileReplacementSource()
    {
        var code = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "BundleExplorerPage.xaml.cs"));
        var handler = code[
            code.IndexOf("private async void OnPrimaryPaneReplaceSelectedAssetRequested", StringComparison.Ordinal)..
            code.IndexOf("private async void OnInspectorSelectFileReplacementSourceRequested", StringComparison.Ordinal)];

        Assert.Contains("HandleMainAssetSelectionChangedAsync", handler);
        Assert.Contains("SelectFileReplacementSourceAsync", handler);
        Assert.DoesNotContain("SelectWorkspaceReplacementSourceAsync", handler);
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
