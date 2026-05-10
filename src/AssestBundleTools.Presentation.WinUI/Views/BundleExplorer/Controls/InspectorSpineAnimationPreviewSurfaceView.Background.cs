using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorSpineAnimationPreviewSurfaceView
{
    private void OnBackgroundModeButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.ToggleBackgroundMode();
        RefreshBackgroundMode();
        RefreshSpineBackgroundActionToolTip();
    }

    private void RefreshBackgroundMode()
    {
        _renderBackend.SetBackgroundMode(ResolveBackgroundMode(ViewModel));
    }

    private static bool ShouldRefreshBackgroundMode(string? propertyName) =>
        propertyName is nameof(InspectorPreviewSpineAnimationViewModel.IsCheckerboardBackgroundSelected) or
            nameof(InspectorPreviewSpineAnimationViewModel.IsSolidBackgroundSelected) or
            nameof(InspectorPreviewSpineAnimationViewModel.BackgroundModeGlyph) or
            nameof(InspectorPreviewSpineAnimationViewModel.CheckerboardBackgroundVisibility) or
            nameof(InspectorPreviewSpineAnimationViewModel.SolidBackgroundVisibility);

    private static bool ShouldRefreshSpineBackgroundActionToolTip(
        InspectorPreviewSpineAnimationViewModel? viewModel,
        bool shouldUpdateBindings) =>
        shouldUpdateBindings && viewModel is not null;

    private static InspectorPreviewImageBackgroundMode ResolveBackgroundMode(
        InspectorPreviewSpineAnimationViewModel? viewModel) =>
        viewModel?.IsCheckerboardBackgroundSelected == true
            ? InspectorPreviewImageBackgroundMode.Checkerboard
            : InspectorPreviewImageBackgroundMode.Solid;

    private void RefreshSpineBackgroundActionToolTip()
    {
        var key = ViewModel?.IsCheckerboardBackgroundSelected == true
            ? ResourceKeys.InspectorPreviewCheckerboardBackgroundToolTip
            : ResourceKeys.InspectorPreviewSolidBackgroundToolTip;
        ToolTipService.SetToolTip(BackgroundModeButton, UiText.Get(key));
    }
}
