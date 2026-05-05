using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed class InspectorPreviewFontViewModel : ObservableObject
{
    private AssetPreviewFontDto? _font;
    private string _assetNameText = string.Empty;
    private string _summaryText = string.Empty;
    private string _metricsText = string.Empty;
    private string _fontDataSizeText = string.Empty;
    private string _textureTitle = string.Empty;
    private string _defaultMaterialTitle = string.Empty;
    private string _textureReferenceText = string.Empty;
    private string _defaultMaterialReferenceText = string.Empty;
    private string _charactersTitle = string.Empty;
    private string _emptyCharactersText = string.Empty;
    private string _truncatedText = string.Empty;
    private IReadOnlyList<FontCharacterRowViewModel> _characterRows = Array.Empty<FontCharacterRowViewModel>();

    public AssetPreviewFontDto? Font
    {
        get => _font;
        private set
        {
            if (SetProperty(ref _font, value))
            {
                OnPropertyChanged(nameof(HasFont));
                OnPropertyChanged(nameof(Visibility));
            }
        }
    }

    public string AssetNameText
    {
        get => _assetNameText;
        private set => SetProperty(ref _assetNameText, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public string MetricsText
    {
        get => _metricsText;
        private set => SetProperty(ref _metricsText, value);
    }

    public string FontDataSizeText
    {
        get => _fontDataSizeText;
        private set
        {
            if (SetProperty(ref _fontDataSizeText, value))
            {
                OnPropertyChanged(nameof(FontDataSizeVisibility));
            }
        }
    }

    public string TextureTitle
    {
        get => _textureTitle;
        private set => SetProperty(ref _textureTitle, value);
    }

    public string DefaultMaterialTitle
    {
        get => _defaultMaterialTitle;
        private set => SetProperty(ref _defaultMaterialTitle, value);
    }

    public string TextureReferenceText
    {
        get => _textureReferenceText;
        private set => SetProperty(ref _textureReferenceText, value);
    }

    public string DefaultMaterialReferenceText
    {
        get => _defaultMaterialReferenceText;
        private set => SetProperty(ref _defaultMaterialReferenceText, value);
    }

    public string CharactersTitle
    {
        get => _charactersTitle;
        private set => SetProperty(ref _charactersTitle, value);
    }

    public string EmptyCharactersText
    {
        get => _emptyCharactersText;
        private set => SetProperty(ref _emptyCharactersText, value);
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

    public IReadOnlyList<FontCharacterRowViewModel> CharacterRows
    {
        get => _characterRows;
        private set
        {
            if (SetProperty(ref _characterRows, value))
            {
                OnPropertyChanged(nameof(CharacterRowsVisibility));
                OnPropertyChanged(nameof(CharacterRowsEmptyVisibility));
            }
        }
    }

    public bool HasFont => Font is not null;

    public Visibility Visibility => HasFont ? Visibility.Visible : Visibility.Collapsed;

    public Visibility FontDataSizeVisibility =>
        string.IsNullOrWhiteSpace(FontDataSizeText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility CharacterRowsVisibility => CharacterRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CharacterRowsEmptyVisibility => CharacterRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TruncatedTextVisibility =>
        string.IsNullOrWhiteSpace(TruncatedText) ? Visibility.Collapsed : Visibility.Visible;

    internal void Apply(
        AssetPreviewFontDto font,
        FontPreviewDisplayState displayState)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(displayState);

        Font = font;
        AssetNameText = displayState.AssetNameText;
        SummaryText = displayState.SummaryText;
        MetricsText = displayState.MetricsText;
        FontDataSizeText = displayState.FontDataSizeText;
        TextureTitle = displayState.TextureTitle;
        DefaultMaterialTitle = displayState.DefaultMaterialTitle;
        TextureReferenceText = displayState.TextureReferenceText;
        DefaultMaterialReferenceText = displayState.DefaultMaterialReferenceText;
        CharactersTitle = displayState.CharactersTitle;
        EmptyCharactersText = displayState.EmptyCharactersText;
        TruncatedText = displayState.TruncatedText;
        CharacterRows = displayState.CharacterRows.Select(static row => new FontCharacterRowViewModel(row)).ToArray();
    }

    internal void Clear()
    {
        Font = null;
        AssetNameText = string.Empty;
        SummaryText = string.Empty;
        MetricsText = string.Empty;
        FontDataSizeText = string.Empty;
        TextureTitle = string.Empty;
        DefaultMaterialTitle = string.Empty;
        TextureReferenceText = string.Empty;
        DefaultMaterialReferenceText = string.Empty;
        CharactersTitle = string.Empty;
        EmptyCharactersText = string.Empty;
        TruncatedText = string.Empty;
        CharacterRows = Array.Empty<FontCharacterRowViewModel>();
    }
}

public sealed class FontCharacterRowViewModel
{
    public FontCharacterRowViewModel(FontCharacterRowDisplayState row)
    {
        CodePointText = row.CodePointText;
        GlyphText = row.GlyphText;
        UvText = row.UvText;
        VertexText = row.VertexText;
        AdvanceText = row.AdvanceText;
        FlippedText = row.FlippedText;
    }

    public string CodePointText { get; }

    public string GlyphText { get; }

    public string UvText { get; }

    public string VertexText { get; }

    public string AdvanceText { get; }

    public string FlippedText { get; }

    public Visibility GlyphVisibility =>
        string.IsNullOrWhiteSpace(GlyphText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility UvVisibility =>
        string.IsNullOrWhiteSpace(UvText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility VertexVisibility =>
        string.IsNullOrWhiteSpace(VertexText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility AdvanceVisibility =>
        string.IsNullOrWhiteSpace(AdvanceText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility FlippedVisibility =>
        string.IsNullOrWhiteSpace(FlippedText) ? Visibility.Collapsed : Visibility.Visible;
}
