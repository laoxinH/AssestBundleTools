using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorPreviewSurfaceView
{
    private void OnOpenPreviewLocationButtonClick(object sender, RoutedEventArgs e)
    {
        OpenPreviewLocationRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPreviewResolvedAssetOpenRequested(
        object? sender,
        ResolvedAssetOpenRequestedEventArgs e)
    {
        ResolvedAssetOpenRequested?.Invoke(this, e);
    }

    private void OnPreviewTextLoadMoreLinesRequested(object? sender, EventArgs e)
    {
        LoadMoreTextRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnBackgroundModeButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.Preview.Image.DisplayOptions.ToggleBackgroundMode();
        RefreshImageActionToolTips();
    }

    private void OnSpriteDisplayModeButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.Preview.Image.DisplayOptions.ToggleSpriteDisplayMode();
        RefreshImageActionToolTips();
    }

    private void OnImageZoomOutButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.Preview.Image.ZoomBy(0.84d);
    }

    private void OnImageZoomInButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.Preview.Image.ZoomBy(1.18d);
    }

    private void OnImageResetButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.Preview.Image.ResetView();
    }

    private void OnSpriteEditResetOriginalButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.Preview.Image.ResetSpriteEditGeometryToOriginal();
    }

    private void OnSpriteEditFullSizeButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.Preview.Image.UseFullImageForSpriteEditGeometry();
    }

    private void OnSpriteEditReplaceTextureButtonClick(object sender, RoutedEventArgs e)
    {
        SpriteTextureReplacementRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnMeshZoomOutButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.Preview.Mesh.ZoomBy(0.84d);
    }

    private void OnMeshDisplayModeButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.Preview.Mesh.ToggleDisplayMode();
        RefreshMeshActionToolTips();
    }

    private void OnMeshTextureOverlayButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.Preview.Mesh.ToggleTextureOverlay();
        RefreshMeshActionToolTips();
    }

    private void OnMeshGridButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.Preview.Mesh.ToggleMeshGrid();
        RefreshMeshActionToolTips();
    }

    private void OnMeshZoomInButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.Preview.Mesh.ZoomBy(1.18d);
    }

    private void OnMeshResetButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.Preview.Mesh.ResetView();
    }

    private void OnPreviewImageViewportChanged(object sender, PreviewImageViewportChangedEventArgs e)
    {
        ViewModel?.UpdatePreviewImageViewport(e.Width, e.Height);
    }

    private void RefreshImageActionToolTips()
    {
        var displayOptions = ViewModel?.Preview.Image.DisplayOptions;
        if (displayOptions is null)
        {
            return;
        }

        ToolTipService.SetToolTip(
            BackgroundModeButton,
            UiText.Get(displayOptions.IsCheckerboardBackgroundSelected
                ? ResourceKeys.InspectorPreviewCheckerboardBackgroundToolTip
                : ResourceKeys.InspectorPreviewSolidBackgroundToolTip));
        ToolTipService.SetToolTip(
            SpriteDisplayModeButton,
            UiText.Get(displayOptions.IsSpriteCropResultSelected
                ? ResourceKeys.InspectorSpriteCropResultModeToolTip
                : ResourceKeys.InspectorSpriteCropPreviewModeToolTip));
    }

    private void RefreshMeshActionToolTips()
    {
        var mesh = ViewModel?.Preview.Mesh;
        if (mesh is null)
        {
            return;
        }

        ToolTipService.SetToolTip(
            MeshDisplayModeButton,
            UiText.Get(mesh.IsUvSelected
                ? ResourceKeys.InspectorMeshPreviewWireframeModeToolTip
                : ResourceKeys.InspectorMeshPreviewUvModeToolTip));
        ToolTipService.SetToolTip(
            MeshTextureOverlayButton,
            UiText.Get(mesh.IsTextureOverlaySelected
                ? ResourceKeys.InspectorMeshPreviewHideTextureOverlayToolTip
                : ResourceKeys.InspectorMeshPreviewShowTextureOverlayToolTip));
        ToolTipService.SetToolTip(
            MeshGridButton,
            UiText.Get(mesh.IsMeshGridSelected
                ? ResourceKeys.InspectorMeshPreviewHideGridToolTip
                : ResourceKeys.InspectorMeshPreviewShowGridToolTip));
    }

    private static void ApplyButtonAssistiveText(Button button, string text)
    {
        ToolTipService.SetToolTip(button, text);
        AutomationProperties.SetName(button, text);
    }
}
