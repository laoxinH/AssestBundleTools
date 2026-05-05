using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 信息卡的可选视觉附件，用于把预览型细节放进资源信息区。
/// </summary>
public sealed class InspectorInfoCardVisualViewModel
{
    private InspectorInfoCardVisualViewModel(
        MaterialPreviewAppearanceViewModel? materialAppearance,
        ImageSource? imageSource,
        Color? imageTintColor)
    {
        MaterialAppearance = materialAppearance;
        ImageSource = imageSource;
        ImageTintColor = imageTintColor;
    }

    private Brush? _imageTintBrush;

    public MaterialPreviewAppearanceViewModel? MaterialAppearance { get; }

    public ImageSource? ImageSource { get; }

    public Color? ImageTintColor { get; }

    public Brush? ImageTintBrush =>
        ImageTintColor.HasValue
            ? _imageTintBrush ??= new SolidColorBrush(ImageTintColor.Value)
            : null;

    public bool HasVisual => MaterialAppearance is { HasAppearance: true } || ImageSource is not null;

    public Visibility MaterialAppearanceVisibility =>
        MaterialAppearance is { HasAppearance: true } ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ImagePreviewVisibility =>
        ImageSource is not null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ImageTintVisibility =>
        ImageTintColor.HasValue ? Visibility.Visible : Visibility.Collapsed;

    public static InspectorInfoCardVisualViewModel CreateMaterialAppearance(
        MaterialPreviewAppearanceDisplayState appearance)
    {
        var viewModel = new MaterialPreviewAppearanceViewModel();
        viewModel.Apply(appearance);
        return new InspectorInfoCardVisualViewModel(viewModel, null, null);
    }

    public static InspectorInfoCardVisualViewModel CreateImagePreview(
        ImageSource imageSource,
        Color? tintColor)
    {
        return new InspectorInfoCardVisualViewModel(null, imageSource, tintColor);
    }
}
