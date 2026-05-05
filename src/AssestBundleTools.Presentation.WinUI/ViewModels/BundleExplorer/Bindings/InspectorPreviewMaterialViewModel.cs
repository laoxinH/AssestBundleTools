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
    private string _shaderResolutionText = string.Empty;
    private string? _resolvedShaderAssetId;
    private ResolvedAssetNavigationTarget? _shaderTarget;
    private string _propertyCountText = string.Empty;
    private string _mainTexturePreviewTitleText = string.Empty;
    private string _mainTexturePreviewNameText = string.Empty;
    private string _mainTexturePreviewResolutionText = string.Empty;
    private string _mainTexturePreviewTransformText = string.Empty;
    private string _mainTexturePreviewTintText = string.Empty;
    private string _mainTexturePreviewLimitText = string.Empty;
    private string _semanticSectionTitle = string.Empty;
    private string _textureSectionTitle = string.Empty;
    private string _textureReferenceSummaryText = string.Empty;
    private string _colorSectionTitle = string.Empty;
    private string _numberSectionTitle = string.Empty;
    private string _emptySectionText = string.Empty;
    private string _openResolvedAssetButtonText = string.Empty;
    private string _openResolvedAssetButtonToolTipText = string.Empty;
    private string _truncatedText = string.Empty;
    private ResolvedAssetNavigationTarget? _mainTexturePreviewTarget;
    private ImageSource? _mainTexturePreviewImageSource;
    private Brush? _mainTexturePreviewTintBrush;
    private MaterialPreviewTintDisplayState? _mainTexturePreviewTint;
    private IReadOnlyList<MaterialPreviewSummaryCardViewModel> _summaryCards = Array.Empty<MaterialPreviewSummaryCardViewModel>();
    private IReadOnlyList<MaterialPreviewSemanticBadgeViewModel> _semanticBadges = Array.Empty<MaterialPreviewSemanticBadgeViewModel>();
    private IReadOnlyList<MaterialPreviewTextureRowViewModel> _textureRows = Array.Empty<MaterialPreviewTextureRowViewModel>();
    private IReadOnlyList<MaterialPreviewColorRowViewModel> _colorRows = Array.Empty<MaterialPreviewColorRowViewModel>();
    private IReadOnlyList<MaterialPreviewNumberRowViewModel> _numberRows = Array.Empty<MaterialPreviewNumberRowViewModel>();

    public MaterialPreviewAppearanceViewModel Appearance { get; } = new();

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

    public string ShaderResolutionText
    {
        get => _shaderResolutionText;
        private set
        {
            if (SetProperty(ref _shaderResolutionText, value))
            {
                OnPropertyChanged(nameof(ShaderResolutionVisibility));
            }
        }
    }

    public string? ResolvedShaderAssetId
    {
        get => _resolvedShaderAssetId;
        private set => SetProperty(ref _resolvedShaderAssetId, value);
    }

    public ResolvedAssetNavigationTarget? ShaderTarget
    {
        get => _shaderTarget;
        private set
        {
            if (SetProperty(ref _shaderTarget, value))
            {
                OnPropertyChanged(nameof(ShaderOpenButtonVisibility));
            }
        }
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

    public IReadOnlyList<MaterialPreviewSummaryCardViewModel> SummaryCards
    {
        get => _summaryCards;
        private set
        {
            if (SetProperty(ref _summaryCards, value))
            {
                OnPropertyChanged(nameof(SummaryCardsVisibility));
            }
        }
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

    public string TextureReferenceSummaryText
    {
        get => _textureReferenceSummaryText;
        private set
        {
            if (SetProperty(ref _textureReferenceSummaryText, value))
            {
                OnPropertyChanged(nameof(TextureReferenceSummaryVisibility));
            }
        }
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

    public string OpenResolvedAssetButtonText
    {
        get => _openResolvedAssetButtonText;
        private set => SetProperty(ref _openResolvedAssetButtonText, value);
    }

    public string OpenResolvedAssetButtonToolTipText
    {
        get => _openResolvedAssetButtonToolTipText;
        private set => SetProperty(ref _openResolvedAssetButtonToolTipText, value);
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

    public Visibility TextureReferenceSummaryVisibility =>
        string.IsNullOrWhiteSpace(TextureReferenceSummaryText) ? Visibility.Collapsed : Visibility.Visible;

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

    public ResolvedAssetNavigationTarget? MainTexturePreviewTarget
    {
        get => _mainTexturePreviewTarget;
        private set
        {
            if (SetProperty(ref _mainTexturePreviewTarget, value))
            {
                OnPropertyChanged(nameof(MainTexturePreviewOpenButtonVisibility));
            }
        }
    }

    public Visibility ShaderOpenButtonVisibility => ShaderTarget is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility MainTexturePreviewOpenButtonVisibility =>
        MainTexturePreviewTarget is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ShaderResolutionVisibility => string.IsNullOrWhiteSpace(ShaderResolutionText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SummaryCardsVisibility =>
        SummaryCards.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

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
        ShaderResolutionText = displayState.ShaderResolutionText;
        ResolvedShaderAssetId = displayState.ResolvedShaderAssetId;
        ShaderTarget = displayState.ShaderTarget;
        PropertyCountText = displayState.PropertyCountText;
        MainTexturePreviewTitleText = displayState.MainTexturePreviewTitleText;
        MainTexturePreviewLimitText = displayState.MainTexturePreviewLimitText;
        MainTexturePreviewTintText = displayState.MainTextureTint?.Text ?? string.Empty;
        ApplyMainTextureTint(displayState.MainTextureTint);
        Appearance.Apply(displayState.Appearance);
        SemanticSectionTitle = displayState.SemanticSectionTitle;
        SummaryCards = (displayState.SummaryCards ?? Array.Empty<MaterialPreviewSummaryCardDisplayState>())
            .Select(static card => new MaterialPreviewSummaryCardViewModel(card))
            .ToArray();
        SemanticBadges = displayState.SemanticBadges.Select(static badge => new MaterialPreviewSemanticBadgeViewModel(badge)).ToArray();
        TextureSectionTitle = displayState.TextureSectionTitle;
        TextureReferenceSummaryText = displayState.TextureReferenceSummaryText;
        ColorSectionTitle = displayState.ColorSectionTitle;
        NumberSectionTitle = displayState.NumberSectionTitle;
        EmptySectionText = displayState.EmptySectionText;
        OpenResolvedAssetButtonText = displayState.OpenResolvedAssetButtonText;
        OpenResolvedAssetButtonToolTipText = displayState.OpenResolvedAssetButtonToolTipText;
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
        ShaderResolutionText = string.Empty;
        ResolvedShaderAssetId = null;
        ShaderTarget = null;
        PropertyCountText = string.Empty;
        MainTexturePreviewTitleText = string.Empty;
        MainTexturePreviewNameText = string.Empty;
        MainTexturePreviewResolutionText = string.Empty;
        MainTexturePreviewTransformText = string.Empty;
        MainTexturePreviewTintText = string.Empty;
        MainTexturePreviewLimitText = string.Empty;
        MainTexturePreviewImageSource = null;
        ApplyMainTextureTint(null);
        Appearance.Clear();
        SemanticSectionTitle = string.Empty;
        SummaryCards = Array.Empty<MaterialPreviewSummaryCardViewModel>();
        SemanticBadges = Array.Empty<MaterialPreviewSemanticBadgeViewModel>();
        TextureSectionTitle = string.Empty;
        TextureReferenceSummaryText = string.Empty;
        ColorSectionTitle = string.Empty;
        NumberSectionTitle = string.Empty;
        EmptySectionText = string.Empty;
        OpenResolvedAssetButtonText = string.Empty;
        OpenResolvedAssetButtonToolTipText = string.Empty;
        TruncatedText = string.Empty;
        MainTexturePreviewTarget = null;
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
        MainTexturePreviewTarget = previewRow?.Target;
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
