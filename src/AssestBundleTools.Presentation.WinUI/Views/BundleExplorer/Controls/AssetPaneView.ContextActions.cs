using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Domain.Assets;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class AssetPaneView
{
    private void OnAssetRowContextFlyoutOpening(object sender, object e)
    {
        var flyout = sender as MenuFlyout;
        ConfigureAssetRowContextFlyout(
            flyout,
            ResolveAssetRowModifyTextResourceKey(flyout));
    }

    private void OnSpineGroupRowContextFlyoutOpening(object sender, object e)
    {
        ConfigureAssetRowContextFlyout(
            sender as MenuFlyout,
            ResourceKeys.AssetPaneViewContextEditText);
    }

    private void OnOpenAssetPaneItemInNewWindowMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (TrySelectContextTarget(sender, preserveExistingSelection: false))
        {
            OpenSelectedItemInNewWindowRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnExtractAssetPaneItemMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (TrySelectContextTarget(sender, preserveExistingSelection: false))
        {
            ExtractSelectedItemRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnReplaceAssetPaneItemMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (TrySelectContextTarget(sender, preserveExistingSelection: false))
        {
            ReplaceSelectedAssetRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnEnterAssetPaneItemMultiSelectionMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (!TrySelectContextTarget(sender, preserveExistingSelection: true))
        {
            return;
        }

        ViewModel?.EnterMultiSelectionMode();
        UpdateListSelectionMode();
        FocusCurrentListView();
    }

    private void ConfigureAssetRowContextFlyout(MenuFlyout? flyout, string replaceCommandTextResourceKey)
    {
        if (flyout is null)
        {
            return;
        }

        var canExecute = ViewModel is not null && !ViewModel.IsBusy;
        foreach (var item in flyout.Items.OfType<MenuFlyoutItem>())
        {
            item.IsEnabled = canExecute;
            switch (item.Name)
            {
                case "OpenAssetPaneItemInNewWindowMenuItem":
                    item.Text = UiText.Get(ResourceKeys.AssetEditorWindowOpenInNewWindowText);
                    break;
                case "ExtractAssetPaneItemMenuItem":
                    item.Text = UiText.Get(ResourceKeys.InspectorExtractButton);
                    break;
                case "ReplaceAssetPaneItemMenuItem":
                    item.Text = UiText.Get(replaceCommandTextResourceKey);
                    item.IsEnabled = canExecute;
                    break;
                case "EnterAssetPaneItemMultiSelectionMenuItem":
                    item.Text = UiText.Get(ResourceKeys.AssetPaneViewContextEnterMultiSelectionText);
                    break;
            }
        }
    }

    private static string ResolveAssetRowModifyTextResourceKey(MenuFlyout? flyout)
    {
        var target = flyout?.Items
            .OfType<MenuFlyoutItem>()
            .Select(static item => item.Tag)
            .OfType<AssetListEntryViewModel>()
            .FirstOrDefault();
        return target?.Item.AssetType == AssetType.Sprite
            ? ResourceKeys.AssetPaneViewContextEditText
            : ResourceKeys.AssetPaneViewContextReplaceText;
    }

    private bool TrySelectContextTarget(
        object sender,
        bool preserveExistingSelection,
        bool allowSpineGroup = true)
    {
        var target = (sender as FrameworkElement)?.Tag;
        if (target is AssetListEntryViewModel assetEntry)
        {
            return _selectionCoordinator.SelectAssetForContextAction(assetEntry, preserveExistingSelection);
        }

        if (allowSpineGroup && target is SpineAssetGroupEntryViewModel spineGroupEntry)
        {
            return _selectionCoordinator.SelectSpineGroupForContextAction(spineGroupEntry, preserveExistingSelection);
        }

        return false;
    }
}
