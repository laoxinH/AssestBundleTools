using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal static class AssetPaneRowVisualState
{
    /// <summary>
    /// 资产列表行的悬停/按压叠层复用左侧资源包列表的反馈语言。
    /// </summary>
    public static void Update(object sender, bool isPointerOver, bool isPressed)
    {
        if (sender is not Grid rowRoot)
        {
            return;
        }

        if (rowRoot.FindName("AssetRowHoverOverlay") is not Border hoverOverlay ||
            rowRoot.FindName("AssetRowPressedOverlay") is not Border pressedOverlay)
        {
            return;
        }

        hoverOverlay.Opacity = isPointerOver && !isPressed ? 1d : 0d;
        pressedOverlay.Opacity = isPressed ? 1d : 0d;
    }
}
