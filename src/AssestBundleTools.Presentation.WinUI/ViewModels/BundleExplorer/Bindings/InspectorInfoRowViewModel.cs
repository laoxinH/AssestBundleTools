using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示检查器信息卡中的单行键值信息。
/// </summary>
public sealed class InspectorInfoRowViewModel
{
    public InspectorInfoRowViewModel(
        string label,
        string value,
        ResolvedAssetNavigationTarget? target = null,
        string openButtonText = "",
        string openButtonToolTipText = "",
        ImageSource? thumbnailImageSource = null,
        bool showThumbnailSlot = false,
        Color? colorSwatchColor = null)
    {
        Label = string.IsNullOrWhiteSpace(label)
            ? string.Empty
            : label.Trim();
        Value = string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
        Target = target;
        OpenButtonText = string.IsNullOrWhiteSpace(openButtonText)
            ? string.Empty
            : openButtonText.Trim();
        OpenButtonToolTipText = string.IsNullOrWhiteSpace(openButtonToolTipText)
            ? string.Empty
            : openButtonToolTipText.Trim();
        ThumbnailImageSource = thumbnailImageSource;
        ShowThumbnailSlot = showThumbnailSlot;
        ColorSwatchColor = colorSwatchColor;
    }

    private Brush? _colorSwatchBrush;

    public string Label { get; }

    public string Value { get; }

    public ResolvedAssetNavigationTarget? Target { get; }

    public string OpenButtonText { get; }

    public string OpenButtonToolTipText { get; }

    public ImageSource? ThumbnailImageSource { get; }

    public bool ShowThumbnailSlot { get; }

    public Color? ColorSwatchColor { get; }

    public Brush? ColorSwatchBrush =>
        ColorSwatchColor.HasValue
            ? _colorSwatchBrush ??= new SolidColorBrush(ColorSwatchColor.Value)
            : null;

    public Visibility LabelVisibility =>
        string.IsNullOrWhiteSpace(Label)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility OpenButtonVisibility =>
        Target is null
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility VisualVisibility =>
        ShowThumbnailSlot || ColorSwatchColor.HasValue
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ThumbnailVisibility =>
        ThumbnailImageSource is null
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility ThumbnailPlaceholderVisibility =>
        ShowThumbnailSlot && ThumbnailImageSource is null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ColorSwatchVisibility =>
        ColorSwatchColor.HasValue
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string CopyText =>
        string.IsNullOrWhiteSpace(Label)
            ? Value
            : $"{Label}: {Value}";
}
