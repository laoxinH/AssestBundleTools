using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class AssetPaneView
{
    /// <summary>
    /// 资产名称提示只在名称列局部生效，并使用更短的延迟打开，
    /// 避免沿用系统默认等待时间导致“已经想确认名字了，但提示还没出来”。
    /// </summary>
    private void OnAssetNamePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not TextBlock textBlock)
        {
            return;
        }

        _assetNameToolTipCoordinator.OpenAfterDelay(textBlock);
    }

    private void OnAssetNamePointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not TextBlock textBlock)
        {
            return;
        }

        _assetNameToolTipCoordinator.CloseFor(textBlock);
    }

    private void OnAssetNamePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not TextBlock textBlock)
        {
            return;
        }

        _assetNameToolTipCoordinator.CloseFor(textBlock);
    }

    private void OnAssetNameTextBlockUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBlock textBlock)
        {
            return;
        }

        _assetNameToolTipCoordinator.CloseFor(textBlock);
    }

    private void OnAssetRowPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        AssetPaneRowVisualState.Update(sender, isPointerOver: true, isPressed: false);
        TryHandleMultiSelectionRowPointerEntered(sender, e);
    }

    private void OnAssetRowPointerExited(object sender, PointerRoutedEventArgs e)
    {
        AssetPaneRowVisualState.Update(sender, isPointerOver: false, isPressed: false);
    }

    private void OnAssetRowPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        AssetPaneRowVisualState.Update(sender, isPointerOver: true, isPressed: true);
        TryHandleMultiSelectionRowPointerPressed(sender, e);
    }

    private void OnAssetRowPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        AssetPaneRowVisualState.Update(sender, isPointerOver: true, isPressed: false);
        EndMultiSelectionDragGesture();
    }

    private void OnAssetRowPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        AssetPaneRowVisualState.Update(sender, isPointerOver: false, isPressed: false);
        EndMultiSelectionDragGesture();
    }
}
