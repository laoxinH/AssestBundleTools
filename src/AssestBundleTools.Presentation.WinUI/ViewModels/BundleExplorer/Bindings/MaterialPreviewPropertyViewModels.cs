using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed class MaterialPreviewSemanticBadgeViewModel
{
    public MaterialPreviewSemanticBadgeViewModel(MaterialPreviewSemanticBadgeDisplayState badge)
    {
        Text = badge.Text;
    }

    public string Text { get; }
}

public sealed class MaterialPreviewTextureRowViewModel
{
    public MaterialPreviewTextureRowViewModel(
        MaterialPreviewTextureRowDisplayState row,
        ImageSource? thumbnailImageSource)
    {
        PropertyName = row.PropertyName;
        TargetText = row.TargetText;
        ResolutionText = row.ResolutionText;
        TransformText = row.TransformText;
        IsMainTextureCandidate = row.IsMainTextureCandidate;
        Target = row.Target;
        OpenButtonText = row.OpenButtonText;
        OpenButtonToolTipText = row.OpenButtonToolTipText;
        ThumbnailImageSource = thumbnailImageSource;
    }

    public string PropertyName { get; }

    public string TargetText { get; }

    public string ResolutionText { get; }

    public string TransformText { get; }

    public bool IsMainTextureCandidate { get; }

    public ResolvedAssetNavigationTarget? Target { get; }

    public string OpenButtonText { get; }

    public string OpenButtonToolTipText { get; }

    public ImageSource? ThumbnailImageSource { get; }

    public Visibility ResolutionVisibility =>
        string.IsNullOrWhiteSpace(ResolutionText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility TransformVisibility =>
        string.IsNullOrWhiteSpace(TransformText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ThumbnailVisibility => ThumbnailImageSource is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ThumbnailPlaceholderVisibility => ThumbnailImageSource is null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility OpenButtonVisibility => Target is null ? Visibility.Collapsed : Visibility.Visible;
}

public sealed class MaterialPreviewColorRowViewModel
{
    private Brush? _swatchBrush;

    public MaterialPreviewColorRowViewModel(MaterialPreviewColorRowDisplayState row)
    {
        PropertyName = row.PropertyName;
        ValueText = row.ValueText;
        Alpha = row.A;
        Red = row.R;
        Green = row.G;
        Blue = row.B;
    }

    public string PropertyName { get; }

    public string ValueText { get; }

    public Brush SwatchBrush =>
        _swatchBrush ??= new SolidColorBrush(Color.FromArgb(
            ToByte(Alpha),
            ToByte(Red),
            ToByte(Green),
            ToByte(Blue)));

    private double Alpha { get; }

    private double Red { get; }

    private double Green { get; }

    private double Blue { get; }

    private static byte ToByte(double value)
    {
        var clamped = Math.Clamp(value, 0d, 1d);
        return (byte)Math.Round(clamped * byte.MaxValue);
    }
}

public sealed class MaterialPreviewNumberRowViewModel
{
    public MaterialPreviewNumberRowViewModel(MaterialPreviewNumberRowDisplayState row)
    {
        PropertyName = row.PropertyName;
        ValueText = row.ValueText;
    }

    public string PropertyName { get; }

    public string ValueText { get; }
}
