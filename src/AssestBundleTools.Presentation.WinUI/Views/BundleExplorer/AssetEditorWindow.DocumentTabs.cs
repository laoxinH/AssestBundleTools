using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class AssetEditorWindow
{
    private async void OnAssetEditorTabViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizingSelection)
        {
            return;
        }

        if (AssetEditorTabView.SelectedItem is AssetDocumentTabItemViewModel documentTab)
        {
            await ViewModel.ActivateDocumentTabAsync(documentTab);
        }
    }

    private async void OnAssetEditorTabViewTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (TryResolveDocumentTab(args.Item, args.Tab, out var documentTab))
        {
            await CloseDocumentTabForUserRequestAsync(documentTab);
        }
    }

    private void OnAssetEditorTabViewTabDragCompleted(TabView sender, TabViewTabDragCompletedEventArgs args)
    {
        if (!TryBuildTabOrderSnapshot(sender, out IReadOnlyList<AssetDocumentTabItemViewModel> orderedTabs))
        {
            return;
        }

        ViewModel.ReorderDocumentTabs(orderedTabs);
        AssetDocumentTabDragCoordinator.CompleteDrag();
    }

    private void OnAssetEditorTabViewTabDragStarting(TabView sender, TabViewTabDragStartingEventArgs args)
    {
        AssetDocumentTabDragCoordinator.StartDrag(this, args);
    }

    private async void OnAssetEditorTabViewTabDroppedOutside(
        TabView sender,
        TabViewTabDroppedOutsideEventArgs args)
    {
        try
        {
            if (TryResolveDocumentTab(args.Item, args.Tab, out var documentTab))
            {
                await _openDocumentInNewWindowAsync(this, documentTab);
            }
        }
        finally
        {
            AssetDocumentTabDragCoordinator.CompleteDrag();
        }
    }

    private void OnAssetEditorTabViewTabStripDragOver(object sender, DragEventArgs e)
    {
        AssetDocumentTabDragCoordinator.HandleDragOver(this, e);
    }

    private async void OnAssetEditorTabViewTabStripDrop(object sender, DragEventArgs e)
    {
        await AssetDocumentTabDragCoordinator.HandleDropAsync(this, e);
    }

    private void OnAssetEditorTabContextFlyoutOpening(object sender, object e)
    {
        if (sender is not MenuFlyout flyout)
        {
            return;
        }

        foreach (var item in flyout.Items.OfType<MenuFlyoutItem>())
        {
            switch (item.Name)
            {
                case "MoveAssetDocumentToMainWindowMenuItem":
                    item.Text = UiText.Get(ResourceKeys.AssetEditorWindowMoveToMainWindowText);
                    item.IsEnabled = ViewModel.DocumentGroup.HasDocumentTabs && !ViewModel.IsBusy;
                    break;
                case "CloseAssetDocumentTabMenuItem":
                    item.Text = UiText.Get(ResourceKeys.AssetEditorWindowTabContextCloseText);
                    item.IsEnabled = ViewModel.DocumentGroup.HasDocumentTabs;
                    break;
            }
        }
    }

    private async void OnMoveAssetDocumentToMainWindowMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedValue(sender, out AssetDocumentTabItemViewModel? documentTab))
        {
            return;
        }

        await ViewModel.MoveDocumentToMainWindowAsync(documentTab);
    }

    private async void OnCloseAssetDocumentTabMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (TryGetTaggedValue(sender, out AssetDocumentTabItemViewModel? documentTab))
        {
            await CloseDocumentTabForUserRequestAsync(documentTab);
        }
    }

    private Task CloseDocumentTabForUserRequestAsync(AssetDocumentTabItemViewModel documentTab)
    {
        return ShouldReturnDocumentsToMainWindowOnClose()
            ? ViewModel.TryMoveDocumentToMainWindowAsync(documentTab)
            : ViewModel.CloseDocumentTabAsync(documentTab);
    }

    private void SynchronizeSelection() =>
        SynchronizeSelection(ViewModel.DocumentGroup.ActiveDocumentTab);

    private void SynchronizeSelection(AssetDocumentTabItemViewModel? activeDocumentTab)
    {
        if (_isSynchronizingSelection)
        {
            return;
        }

        _isSynchronizingSelection = true;
        try
        {
            if (!ReferenceEquals(AssetEditorTabView.SelectedItem, activeDocumentTab))
            {
                AssetEditorTabView.SelectedItem = activeDocumentTab;
            }
        }
        finally
        {
            _isSynchronizingSelection = false;
        }
    }

    private static bool TryBuildTabOrderSnapshot<TTabViewModel>(
        TabView tabView,
        out IReadOnlyList<TTabViewModel> orderedTabs)
        where TTabViewModel : class
    {
        ArgumentNullException.ThrowIfNull(tabView);

        var resolvedItems = new List<TTabViewModel>(tabView.TabItems.Count);
        foreach (var item in tabView.TabItems)
        {
            if (item is TTabViewModel typedItem)
            {
                resolvedItems.Add(typedItem);
                continue;
            }

            if (item is FrameworkElement { DataContext: TTabViewModel dataContextItem })
            {
                resolvedItems.Add(dataContextItem);
                continue;
            }

            orderedTabs = Array.Empty<TTabViewModel>();
            return false;
        }

        orderedTabs = resolvedItems;
        return true;
    }

    private static bool TryGetTaggedValue<TValue>(
        object sender,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TValue? value)
        where TValue : class
    {
        value = (sender as FrameworkElement)?.Tag as TValue;
        return value is not null;
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

    bool IAssetDocumentTabDragHost.CanStartAssetDocumentTabDrag(AssetDocumentTabItemViewModel documentTab)
    {
        ArgumentNullException.ThrowIfNull(documentTab);

        return !ViewModel.IsBusy &&
               ViewModel.DocumentGroup.DocumentTabs.Contains(documentTab);
    }

    bool IAssetDocumentTabDragHost.CanAcceptAssetDocumentTabDrag(AssetDocumentTabDragSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return !ViewModel.IsBusy;
    }

    Task<AssetDocumentTabItemViewModel?> IAssetDocumentTabDragHost.DetachAssetDocumentTabForDragAsync(
        AssetDocumentTabItemViewModel documentTab) =>
        ViewModel.DetachDocumentTabForExternalHostAsync(documentTab);

    Task<bool> IAssetDocumentTabDragHost.AttachAssetDocumentTabFromDragAsync(
        AssetDocumentTabItemViewModel documentTab) =>
        ViewModel.AttachAssetDocumentGroupFromExternalHostAsync(
            new AssetDocumentGroupTransfer([documentTab], documentTab));

    Task IAssetDocumentTabDragHost.CompleteAssetDocumentTabDragMoveAsync() =>
        ViewModel.CompleteExternalAssetDocumentMoveAsync();

    bool IAssetDocumentGroupDragHost.CanStartAssetDocumentGroupDrag()
    {
        return !ViewModel.IsBusy &&
               ViewModel.DocumentGroup.HasDocumentTabs;
    }

    bool IAssetDocumentGroupDragHost.CanAcceptAssetDocumentGroupDrag(AssetDocumentGroupDragSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return !ViewModel.IsBusy;
    }

    Task<AssetDocumentGroupTransfer?> IAssetDocumentGroupDragHost.DetachAssetDocumentGroupForDragAsync() =>
        ViewModel.DetachAssetDocumentGroupForExternalHostAsync();

    Task<bool> IAssetDocumentGroupDragHost.AttachAssetDocumentGroupFromDragAsync(
        AssetDocumentGroupTransfer transfer) =>
        ViewModel.AttachAssetDocumentGroupFromExternalHostAsync(transfer);

    Task IAssetDocumentGroupDragHost.CompleteAssetDocumentGroupDragMoveAsync() =>
        ViewModel.CompleteExternalAssetDocumentMoveAsync();

    Task IAssetDocumentGroupDragHost.OpenAssetDocumentGroupInNewWindowFromDragAsync() =>
        _openAssetDocumentGroupInNewWindowAsync(this);
}
