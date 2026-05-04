using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed class MaterialPreviewAppearanceViewModel : ObservableObject
{
    private string _titleText = string.Empty;
    private string _noteText = string.Empty;
    private string _baseColorText = string.Empty;
    private string _alphaText = string.Empty;
    private string _metallicText = string.Empty;
    private string _smoothnessText = string.Empty;
    private Color? _sampleColor;
    private Brush? _sampleBrush;
    private double _highlightOpacity;
    private double _metallicSheenOpacity;
    private double _alphaOverlayOpacity;
    private double _emissionGlowOpacity;

    public string TitleText
    {
        get => _titleText;
        private set => SetProperty(ref _titleText, value);
    }

    public string NoteText
    {
        get => _noteText;
        private set => SetProperty(ref _noteText, value);
    }

    public string BaseColorText
    {
        get => _baseColorText;
        private set => SetProperty(ref _baseColorText, value);
    }

    public string AlphaText
    {
        get => _alphaText;
        private set => SetProperty(ref _alphaText, value);
    }

    public string MetallicText
    {
        get => _metallicText;
        private set => SetProperty(ref _metallicText, value);
    }

    public string SmoothnessText
    {
        get => _smoothnessText;
        private set => SetProperty(ref _smoothnessText, value);
    }

    public Brush? SampleBrush => _sampleColor.HasValue
        ? _sampleBrush ??= new SolidColorBrush(_sampleColor.Value)
        : null;

    public double HighlightOpacity
    {
        get => _highlightOpacity;
        private set => SetProperty(ref _highlightOpacity, value);
    }

    public double MetallicSheenOpacity
    {
        get => _metallicSheenOpacity;
        private set => SetProperty(ref _metallicSheenOpacity, value);
    }

    public double AlphaOverlayOpacity
    {
        get => _alphaOverlayOpacity;
        private set => SetProperty(ref _alphaOverlayOpacity, value);
    }

    public double EmissionGlowOpacity
    {
        get => _emissionGlowOpacity;
        private set => SetProperty(ref _emissionGlowOpacity, value);
    }

    public bool HasAppearance => _sampleColor.HasValue;

    public Visibility Visibility => HasAppearance ? Visibility.Visible : Visibility.Collapsed;

    internal void Apply(MaterialPreviewAppearanceDisplayState? state)
    {
        if (state is null)
        {
            Clear();
            return;
        }

        TitleText = state.TitleText;
        NoteText = state.NoteText;
        BaseColorText = state.BaseColorText;
        AlphaText = state.AlphaText;
        MetallicText = state.MetallicText;
        SmoothnessText = state.SmoothnessText;
        _sampleColor = Color.FromArgb(byte.MaxValue, ToByte(state.R), ToByte(state.G), ToByte(state.B));
        _sampleBrush = null;
        OnPropertyChanged(nameof(SampleBrush));
        HighlightOpacity = 0.18d + (Math.Clamp(state.Smoothness, 0d, 1d) * 0.56d);
        MetallicSheenOpacity = Math.Clamp(state.Metallic, 0d, 1d) * 0.42d;
        AlphaOverlayOpacity = 1d - Math.Clamp(state.A, 0d, 1d);
        EmissionGlowOpacity = Math.Clamp(state.EmissionStrength, 0d, 1d) * 0.55d;
        OnPropertyChanged(nameof(HasAppearance));
        OnPropertyChanged(nameof(Visibility));
    }

    internal void Clear()
    {
        TitleText = string.Empty;
        NoteText = string.Empty;
        BaseColorText = string.Empty;
        AlphaText = string.Empty;
        MetallicText = string.Empty;
        SmoothnessText = string.Empty;
        _sampleColor = null;
        _sampleBrush = null;
        OnPropertyChanged(nameof(SampleBrush));
        HighlightOpacity = 0d;
        MetallicSheenOpacity = 0d;
        AlphaOverlayOpacity = 0d;
        EmissionGlowOpacity = 0d;
        OnPropertyChanged(nameof(HasAppearance));
        OnPropertyChanged(nameof(Visibility));
    }

    private static byte ToByte(double value)
    {
        var clamped = Math.Clamp(value, 0d, 1d);
        return (byte)Math.Round(clamped * byte.MaxValue);
    }
}

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
