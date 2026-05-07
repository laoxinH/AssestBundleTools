using System.Collections.Generic;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private async void OnSpineGroupDocumentTabViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var spineGroupTab = _tabCoordinator.GetActivatableSpineGroupDocumentTab();
        if (spineGroupTab is not null)
        {
            await ViewModel.ActivateSpineGroupDocumentTabAsync(spineGroupTab);
        }
    }

    private async void OnSpineGroupDocumentTabViewTabCloseRequested(
        TabView sender,
        TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Item is SpineAssetGroupDocumentTabViewModel spineGroupTab)
        {
            await ViewModel.CloseSpineGroupDocumentTabAsync(spineGroupTab);
            return;
        }

        if (args.Tab is FrameworkElement { DataContext: SpineAssetGroupDocumentTabViewModel dataContextTab })
        {
            await ViewModel.CloseSpineGroupDocumentTabAsync(dataContextTab);
        }
    }

    private void OnSpineGroupDocumentTabViewTabDragCompleted(
        TabView sender,
        TabViewTabDragCompletedEventArgs args)
    {
        if (!_tabCoordinator.TryBuildTabOrderSnapshot(
                sender,
                out IReadOnlyList<SpineAssetGroupDocumentTabViewModel> orderedTabs))
        {
            return;
        }

        ViewModel.ReorderActiveSpineGroupDocumentTabs(orderedTabs);
    }

    private void OnSpineGroupDocumentTabContextFlyoutOpening(object sender, object e)
    {
        if (sender is not MenuFlyout flyout)
        {
            return;
        }

        foreach (var item in flyout.Items.OfType<MenuFlyoutItem>())
        {
            if (item.Name == "OpenSpineGroupDocumentInNewWindowMenuItem")
            {
                item.Text = UiText.Get(ResourceKeys.AssetEditorWindowOpenInNewWindowText);
                item.IsEnabled = !ViewModel.IsBusy;
            }
        }
    }

    private async void OnOpenSpineGroupDocumentInNewWindowMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (_tabCoordinator.TryGetTaggedValue(sender, out SpineAssetGroupDocumentTabViewModel? spineGroupTab))
        {
            await _assetEditorWindowCoordinator.OpenSpineGroupDocumentInNewWindowAsync(spineGroupTab);
        }
    }

    private async void OnInspectorSpineAtlasPageSelectionRequested(
        object sender,
        SpineAtlasPageSelectionRequestedEventArgs e)
    {
        await ViewModel.SelectActiveSpineGroupAtlasPageAsync(e.Option);
    }
}
