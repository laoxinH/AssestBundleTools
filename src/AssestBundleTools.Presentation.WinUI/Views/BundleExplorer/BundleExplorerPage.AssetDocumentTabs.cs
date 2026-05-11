using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private async void OnAssetDocumentTabViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var documentTab = _tabCoordinator.GetActivatableDocumentTab();
        if (documentTab?.AssetTab is { } assetTab)
        {
            await ViewModel.ActivateAssetDocumentTabAsync(assetTab);
        }
        else if (documentTab?.SpineGroupTab is { } spineGroupTab)
        {
            await ViewModel.ActivateSpineGroupDocumentTabAsync(spineGroupTab);
        }
    }

    private async void OnAssetDocumentTabViewTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (TryResolveDocumentTab(args.Item, args.Tab, out var documentTab))
        {
            await CloseDocumentTabAsync(documentTab);
        }
    }

    private void OnAssetDocumentTabViewTabDragCompleted(TabView sender, TabViewTabDragCompletedEventArgs args)
    {
        if (!_tabCoordinator.TryBuildTabOrderSnapshot(sender, out IReadOnlyList<AssetDocumentTabItemViewModel> orderedTabs))
        {
            AssetDocumentTabDragCoordinator.CompleteDrag();
            return;
        }

        ViewModel.ReorderActiveDocumentTabs(orderedTabs);
        AssetDocumentTabDragCoordinator.CompleteDrag();
    }

    private void OnAssetDocumentTabViewTabDragStarting(TabView sender, TabViewTabDragStartingEventArgs args)
    {
        AssetDocumentTabDragCoordinator.StartDrag(this, args);
    }

    private async void OnAssetDocumentTabViewTabDroppedOutside(
        TabView sender,
        TabViewTabDroppedOutsideEventArgs args)
    {
        try
        {
            if (TryResolveDocumentTab(args.Item, args.Tab, out var documentTab))
            {
                await _assetEditorWindowCoordinator.OpenDocumentInNewWindowAsync(documentTab);
            }
        }
        finally
        {
            AssetDocumentTabDragCoordinator.CompleteDrag();
        }
    }

    private void OnAssetDocumentTabViewTabStripDragOver(object sender, DragEventArgs e)
    {
        AssetDocumentTabDragCoordinator.HandleDragOver(this, e);
    }

    private async void OnAssetDocumentTabViewTabStripDrop(object sender, DragEventArgs e)
    {
        await AssetDocumentTabDragCoordinator.HandleDropAsync(this, e);
    }

    private void OnAssetDocumentTabContextFlyoutOpening(object sender, object e)
    {
        _tabCoordinator.ConfigureTabContextFlyout(
            sender as MenuFlyout,
            openInNewWindowMenuItemName: "OpenAssetDocumentInNewWindowMenuItem",
            closeOtherMenuItemName: "CloseOtherAssetDocumentTabsMenuItem",
            closeAllMenuItemName: "CloseAllAssetDocumentTabsMenuItem",
            tabCount: ViewModel.DocumentWorkspace.CurrentDocumentTabs.Count,
            canOpenInNewWindow: !ViewModel.IsBusy);
    }

    private async void OnOpenAssetDocumentInNewWindowMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (_tabCoordinator.TryGetTaggedValue(sender, out AssetDocumentTabItemViewModel? documentTab))
        {
            await _assetEditorWindowCoordinator.OpenDocumentInNewWindowAsync(documentTab);
        }
    }

    private async void OnOpenAssetDocumentGroupInNewWindowButtonClick(object sender, RoutedEventArgs e)
    {
        await _assetEditorWindowCoordinator.OpenActiveAssetDocumentGroupInNewWindowAsync();
    }

    private async void OnLocateActiveDocumentButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.LocateActiveDocumentInPrimaryPaneAsync();
        PrimaryAssetPaneView.ScrollSelectedItemIntoView();
        _tabCoordinator.ScrollActiveDocumentTabIntoView();
    }

    private async void OnCloseOtherAssetDocumentTabsMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (!_tabCoordinator.TryGetTaggedValue(sender, out AssetDocumentTabItemViewModel? documentTab))
        {
            return;
        }

        var tabsToClose = ViewModel.DocumentWorkspace.CurrentDocumentTabs
            .Where(tab => !ReferenceEquals(tab, documentTab))
            .ToArray();
        foreach (var tabToClose in tabsToClose)
        {
            await CloseDocumentTabAsync(tabToClose);
        }

        await ActivateDocumentTabAsync(documentTab);
    }

    private async void OnCloseAllAssetDocumentTabsMenuItemClick(object sender, RoutedEventArgs e)
    {
        foreach (var documentTab in ViewModel.DocumentWorkspace.CurrentDocumentTabs.ToArray())
        {
            await CloseDocumentTabAsync(documentTab);
        }
    }

    private void OnAssetDocumentGroupDragStarting(UIElement sender, DragStartingEventArgs args)
    {
        AssetDocumentGroupDragCoordinator.StartDrag(this, args);
    }

    private async void OnAssetDocumentGroupDropCompleted(UIElement sender, DropCompletedEventArgs args)
    {
        SetAssetDocumentGroupDropPreviewVisible(false);
        await AssetDocumentGroupDragCoordinator.HandleDropCompletedAsync(this, args);
    }

    private void OnAssetDocumentGroupDragEnter(object sender, DragEventArgs e)
    {
        SetAssetDocumentGroupDropPreviewVisible(CanShowAssetDocumentHostDropPreview(e));
    }

    private void OnAssetDocumentGroupDragLeave(object sender, DragEventArgs e)
    {
        SetAssetDocumentGroupDropPreviewVisible(false);
    }

    private void OnAssetDocumentGroupDragOver(object sender, DragEventArgs e)
    {
        if (AssetDocumentTabDragCoordinator.CanShowDropPreview(this, e))
        {
            SetAssetDocumentGroupDropPreviewVisible(true);
            AssetDocumentTabDragCoordinator.HandleDragOver(this, e);
            return;
        }

        SetAssetDocumentGroupDropPreviewVisible(
            AssetDocumentGroupDragCoordinator.CanShowDropPreview(this, e));
        AssetDocumentGroupDragCoordinator.HandleDragOver(this, e);
    }

    private async void OnAssetDocumentGroupDrop(object sender, DragEventArgs e)
    {
        SetAssetDocumentGroupDropPreviewVisible(false);
        if (await AssetDocumentTabDragCoordinator.HandleDropAsync(this, e))
        {
            return;
        }

        await AssetDocumentGroupDragCoordinator.HandleDropAsync(this, e);
    }

    private Task ActivateDocumentTabAsync(AssetDocumentTabItemViewModel documentTab)
    {
        ArgumentNullException.ThrowIfNull(documentTab);

        return documentTab.AssetTab is { } assetTab
            ? ViewModel.ActivateAssetDocumentTabAsync(assetTab)
            : documentTab.SpineGroupTab is { } spineGroupTab
                ? ViewModel.ActivateSpineGroupDocumentTabAsync(spineGroupTab)
                : Task.CompletedTask;
    }

    private Task CloseDocumentTabAsync(AssetDocumentTabItemViewModel documentTab)
    {
        ArgumentNullException.ThrowIfNull(documentTab);

        return documentTab.AssetTab is { } assetTab
            ? ViewModel.CloseAssetDocumentTabAsync(assetTab)
            : documentTab.SpineGroupTab is { } spineGroupTab
                ? ViewModel.CloseSpineGroupDocumentTabAsync(spineGroupTab)
                : Task.CompletedTask;
    }

    private bool CanShowAssetDocumentHostDropPreview(DragEventArgs e) =>
        AssetDocumentTabDragCoordinator.CanShowDropPreview(this, e) ||
        AssetDocumentGroupDragCoordinator.CanShowDropPreview(this, e);

    private void SetAssetDocumentGroupDropPreviewVisible(bool isVisible)
    {
        AssetDocumentGroupDropPreviewOverlay.Visibility = isVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static bool TryResolveDocumentTab(
        object? item,
        TabViewItem? tab,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AssetDocumentTabItemViewModel? documentTab)
    {
        documentTab = item as AssetDocumentTabItemViewModel ??
                      tab?.DataContext as AssetDocumentTabItemViewModel;
        return documentTab is not null;
    }
}
