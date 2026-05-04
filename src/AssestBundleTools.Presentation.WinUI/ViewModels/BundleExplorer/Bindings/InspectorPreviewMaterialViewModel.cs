using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed class InspectorPreviewMaterialViewModel : ObservableObject
{
    private AssetPreviewMaterialDto? _material;
    private string _assetNameText = string.Empty;
    private string _shaderNameText = string.Empty;
    private string _shaderReferenceText = string.Empty;
    private string _propertyCountText = string.Empty;
    private string _mainTexturePreviewTitleText = string.Empty;
    private string _mainTexturePreviewNameText = string.Empty;
    private string _mainTexturePreviewResolutionText = string.Empty;
    private string _mainTexturePreviewTransformText = string.Empty;
    private string _mainTexturePreviewTintText = string.Empty;
    private string _mainTexturePreviewLimitText = string.Empty;
    private string _semanticSectionTitle = string.Empty;
    private string _textureSectionTitle = string.Empty;
    private string _colorSectionTitle = string.Empty;
    private string _numberSectionTitle = string.Empty;
    private string _emptySectionText = string.Empty;
    private string _truncatedText = string.Empty;
    private ImageSource? _mainTexturePreviewImageSource;
    private Brush? _mainTexturePreviewTintBrush;
    private MaterialPreviewTintDisplayState? _mainTexturePreviewTint;
    private IReadOnlyList<MaterialPreviewSemanticBadgeViewModel> _semanticBadges = Array.Empty<MaterialPreviewSemanticBadgeViewModel>();
    private IReadOnlyList<MaterialPreviewTextureRowViewModel> _textureRows = Array.Empty<MaterialPreviewTextureRowViewModel>();
    private IReadOnlyList<MaterialPreviewColorRowViewModel> _colorRows = Array.Empty<MaterialPreviewColorRowViewModel>();
    private IReadOnlyList<MaterialPreviewNumberRowViewModel> _numberRows = Array.Empty<MaterialPreviewNumberRowViewModel>();

    public AssetPreviewMaterialDto? Material
    {
        get => _material;
        private set
        {
            if (SetProperty(ref _material, value))
            {
                OnPropertyChanged(nameof(HasMaterial));
                OnPropertyChanged(nameof(Visibility));
            }
        }
    }

    public string AssetNameText
    {
        get => _assetNameText;
        private set => SetProperty(ref _assetNameText, value);
    }

    public string ShaderNameText
    {
        get => _shaderNameText;
        private set => SetProperty(ref _shaderNameText, value);
    }

    public string ShaderReferenceText
    {
        get => _shaderReferenceText;
        private set => SetProperty(ref _shaderReferenceText, value);
    }

    public string PropertyCountText
    {
        get => _propertyCountText;
        private set => SetProperty(ref _propertyCountText, value);
    }

    public string MainTexturePreviewTitleText
    {
        get => _mainTexturePreviewTitleText;
        private set => SetProperty(ref _mainTexturePreviewTitleText, value);
    }

    public string MainTexturePreviewNameText
    {
        get => _mainTexturePreviewNameText;
        private set => SetProperty(ref _mainTexturePreviewNameText, value);
    }

    public string MainTexturePreviewResolutionText
    {
        get => _mainTexturePreviewResolutionText;
        private set => SetProperty(ref _mainTexturePreviewResolutionText, value);
    }

    public string MainTexturePreviewTransformText
    {
        get => _mainTexturePreviewTransformText;
        private set
        {
            if (SetProperty(ref _mainTexturePreviewTransformText, value))
            {
                OnPropertyChanged(nameof(MainTexturePreviewTransformVisibility));
            }
        }
    }

    public string MainTexturePreviewTintText
    {
        get => _mainTexturePreviewTintText;
        private set
        {
            if (SetProperty(ref _mainTexturePreviewTintText, value))
            {
                OnPropertyChanged(nameof(MainTexturePreviewTintTextVisibility));
            }
        }
    }

    public string MainTexturePreviewLimitText
    {
        get => _mainTexturePreviewLimitText;
        private set
        {
            if (SetProperty(ref _mainTexturePreviewLimitText, value))
            {
                OnPropertyChanged(nameof(MainTexturePreviewLimitVisibility));
            }
        }
    }

    public ImageSource? MainTexturePreviewImageSource
    {
        get => _mainTexturePreviewImageSource;
        private set
        {
            if (SetProperty(ref _mainTexturePreviewImageSource, value))
            {
                OnPropertyChanged(nameof(MainTexturePreviewVisibility));
            }
        }
    }

    public Brush? MainTexturePreviewTintBrush =>
        _mainTexturePreviewTint is null ? null : _mainTexturePreviewTintBrush ??= CreateTintBrush(_mainTexturePreviewTint);

    public string SemanticSectionTitle
    {
        get => _semanticSectionTitle;
        private set => SetProperty(ref _semanticSectionTitle, value);
    }

    public IReadOnlyList<MaterialPreviewSemanticBadgeViewModel> SemanticBadges
    {
        get => _semanticBadges;
        private set
        {
            if (SetProperty(ref _semanticBadges, value))
            {
                OnPropertyChanged(nameof(SemanticHintsVisibility));
            }
        }
    }

    public string TextureSectionTitle
    {
        get => _textureSectionTitle;
        private set => SetProperty(ref _textureSectionTitle, value);
    }

    public string ColorSectionTitle
    {
        get => _colorSectionTitle;
        private set => SetProperty(ref _colorSectionTitle, value);
    }

    public string NumberSectionTitle
    {
        get => _numberSectionTitle;
        private set => SetProperty(ref _numberSectionTitle, value);
    }

    public string EmptySectionText
    {
        get => _emptySectionText;
        private set => SetProperty(ref _emptySectionText, value);
    }

    public string TruncatedText
    {
        get => _truncatedText;
        private set
        {
            if (SetProperty(ref _truncatedText, value))
            {
                OnPropertyChanged(nameof(TruncatedTextVisibility));
            }
        }
    }

    public IReadOnlyList<MaterialPreviewTextureRowViewModel> TextureRows
    {
        get => _textureRows;
        private set
        {
            if (SetProperty(ref _textureRows, value))
            {
                OnPropertyChanged(nameof(TextureRowsVisibility));
                OnPropertyChanged(nameof(TextureRowsEmptyVisibility));
            }
        }
    }

    public IReadOnlyList<MaterialPreviewColorRowViewModel> ColorRows
    {
        get => _colorRows;
        private set
        {
            if (SetProperty(ref _colorRows, value))
            {
                OnPropertyChanged(nameof(ColorRowsVisibility));
                OnPropertyChanged(nameof(ColorRowsEmptyVisibility));
            }
        }
    }

    public IReadOnlyList<MaterialPreviewNumberRowViewModel> NumberRows
    {
        get => _numberRows;
        private set
        {
            if (SetProperty(ref _numberRows, value))
            {
                OnPropertyChanged(nameof(NumberRowsVisibility));
                OnPropertyChanged(nameof(NumberRowsEmptyVisibility));
            }
        }
    }

    public bool HasMaterial => Material is not null;

    public Visibility Visibility => HasMaterial ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TextureRowsVisibility => TextureRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TextureRowsEmptyVisibility => TextureRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ColorRowsVisibility => ColorRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ColorRowsEmptyVisibility => ColorRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NumberRowsVisibility => NumberRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NumberRowsEmptyVisibility => NumberRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility MainTexturePreviewVisibility =>
        MainTexturePreviewImageSource is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility MainTexturePreviewTransformVisibility =>
        string.IsNullOrWhiteSpace(MainTexturePreviewTransformText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility MainTexturePreviewTintTextVisibility =>
        string.IsNullOrWhiteSpace(MainTexturePreviewTintText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility MainTexturePreviewTintVisibility =>
        _mainTexturePreviewTint is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility MainTexturePreviewLimitVisibility =>
        string.IsNullOrWhiteSpace(MainTexturePreviewLimitText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SemanticHintsVisibility =>
        SemanticBadges.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TruncatedTextVisibility =>
        string.IsNullOrWhiteSpace(TruncatedText) ? Visibility.Collapsed : Visibility.Visible;

    internal void Apply(
        AssetPreviewMaterialDto material,
        MaterialPreviewDisplayState displayState,
        IReadOnlyDictionary<string, ImageSource>? textureThumbnailSources = null)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(displayState);

        Material = material;
        AssetNameText = displayState.AssetNameText;
        ShaderNameText = displayState.ShaderNameText;
        ShaderReferenceText = displayState.ShaderReferenceText;
        PropertyCountText = displayState.PropertyCountText;
        MainTexturePreviewTitleText = displayState.MainTexturePreviewTitleText;
        MainTexturePreviewLimitText = displayState.MainTexturePreviewLimitText;
        MainTexturePreviewTintText = displayState.MainTextureTint?.Text ?? string.Empty;
        ApplyMainTextureTint(displayState.MainTextureTint);
        SemanticSectionTitle = displayState.SemanticSectionTitle;
        SemanticBadges = displayState.SemanticBadges.Select(static badge => new MaterialPreviewSemanticBadgeViewModel(badge)).ToArray();
        TextureSectionTitle = displayState.TextureSectionTitle;
        ColorSectionTitle = displayState.ColorSectionTitle;
        NumberSectionTitle = displayState.NumberSectionTitle;
        EmptySectionText = displayState.EmptySectionText;
        TruncatedText = displayState.TruncatedText;
        var textureRows = displayState.TextureRows
            .Select(row => new MaterialPreviewTextureRowViewModel(row, ResolveThumbnailSource(row, textureThumbnailSources)))
            .ToArray();
        TextureRows = textureRows;
        ApplyMainTexturePreview(textureRows);
        ColorRows = displayState.ColorRows.Select(static row => new MaterialPreviewColorRowViewModel(row)).ToArray();
        NumberRows = displayState.NumberRows.Select(static row => new MaterialPreviewNumberRowViewModel(row)).ToArray();
    }

    internal void Clear()
    {
        Material = null;
        AssetNameText = string.Empty;
        ShaderNameText = string.Empty;
        ShaderReferenceText = string.Empty;
        PropertyCountText = string.Empty;
        MainTexturePreviewTitleText = string.Empty;
        MainTexturePreviewNameText = string.Empty;
        MainTexturePreviewResolutionText = string.Empty;
        MainTexturePreviewTransformText = string.Empty;
        MainTexturePreviewTintText = string.Empty;
        MainTexturePreviewLimitText = string.Empty;
        MainTexturePreviewImageSource = null;
        ApplyMainTextureTint(null);
        SemanticSectionTitle = string.Empty;
        SemanticBadges = Array.Empty<MaterialPreviewSemanticBadgeViewModel>();
        TextureSectionTitle = string.Empty;
        ColorSectionTitle = string.Empty;
        NumberSectionTitle = string.Empty;
        EmptySectionText = string.Empty;
        TruncatedText = string.Empty;
        TextureRows = Array.Empty<MaterialPreviewTextureRowViewModel>();
        ColorRows = Array.Empty<MaterialPreviewColorRowViewModel>();
        NumberRows = Array.Empty<MaterialPreviewNumberRowViewModel>();
    }

    private static ImageSource? ResolveThumbnailSource(
        MaterialPreviewTextureRowDisplayState row,
        IReadOnlyDictionary<string, ImageSource>? textureThumbnailSources)
    {
        return !string.IsNullOrWhiteSpace(row.ResolvedTextureAssetId) &&
               textureThumbnailSources is not null &&
               textureThumbnailSources.TryGetValue(row.ResolvedTextureAssetId, out var thumbnailSource)
            ? thumbnailSource
            : null;
    }

    private void ApplyMainTexturePreview(IReadOnlyList<MaterialPreviewTextureRowViewModel> textureRows)
    {
        var previewRow = textureRows.FirstOrDefault(static row =>
                             row.IsMainTextureCandidate &&
                             row.ThumbnailImageSource is not null)
                         ?? textureRows.FirstOrDefault(static row => row.ThumbnailImageSource is not null);

        MainTexturePreviewImageSource = previewRow?.ThumbnailImageSource;
        MainTexturePreviewNameText = previewRow?.PropertyName ?? string.Empty;
        MainTexturePreviewResolutionText = previewRow?.ResolutionText ?? string.Empty;
        MainTexturePreviewTransformText = previewRow?.TransformText ?? string.Empty;
    }

    private void ApplyMainTextureTint(MaterialPreviewTintDisplayState? tint)
    {
        _mainTexturePreviewTint = tint;
        _mainTexturePreviewTintBrush = null;
        OnPropertyChanged(nameof(MainTexturePreviewTintBrush));
        OnPropertyChanged(nameof(MainTexturePreviewTintVisibility));
    }

    private static Brush CreateTintBrush(MaterialPreviewTintDisplayState tint)
    {
        return new SolidColorBrush(Color.FromArgb(
            ToByte(tint.A),
            ToByte(tint.R),
            ToByte(tint.G),
            ToByte(tint.B)));
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
        ThumbnailImageSource = thumbnailImageSource;
    }

    public string PropertyName { get; }

    public string TargetText { get; }

    public string ResolutionText { get; }

    public string TransformText { get; }

    public bool IsMainTextureCandidate { get; }

    public ImageSource? ThumbnailImageSource { get; }

    public Visibility ResolutionVisibility =>
        string.IsNullOrWhiteSpace(ResolutionText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility TransformVisibility =>
        string.IsNullOrWhiteSpace(TransformText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ThumbnailVisibility => ThumbnailImageSource is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ThumbnailPlaceholderVisibility => ThumbnailImageSource is null ? Visibility.Visible : Visibility.Collapsed;
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
