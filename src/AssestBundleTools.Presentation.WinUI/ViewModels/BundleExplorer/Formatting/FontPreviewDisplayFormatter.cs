using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal sealed class FontPreviewDisplayFormatter
{
    private const string UnknownValue = "-";
    private readonly ILocalizationService _localizationService;
    private readonly LocalizedValueFormatter _localizedValueFormatter;

    public FontPreviewDisplayFormatter(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _localizedValueFormatter = new LocalizedValueFormatter(_localizationService);
    }

    public FontPreviewDisplayState BuildDisplayState(
        AssetPreviewFontDto font,
        string assetName)
    {
        ArgumentNullException.ThrowIfNull(font);

        return new FontPreviewDisplayState(
            AssetNameText: ResolveAssetName(font, assetName),
            SummaryText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorFontPreviewSummaryFormat,
                font.CharacterCount.ToString(CultureInfo.CurrentCulture),
                FormatInteger(font.DefaultSize),
                FormatNumber(font.LineSpacing)),
            MetricsText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorFontPreviewMetricsFormat,
                FormatNumber(font.Ascent),
                FormatNumber(font.Descent),
                FormatNumber(font.Tracking),
                FormatInteger(font.CharacterSpacing),
                FormatInteger(font.CharacterPadding)),
            FontDataSizeText: font.FontDataSizeInBytes is > 0
                ? _localizationService.GetFormattedString(
                    ResourceKeys.InspectorFontPreviewDataSizeFormat,
                    _localizedValueFormatter.FormatDetailedByteSize(font.FontDataSizeInBytes.Value))
                : string.Empty,
            TextureTitle: _localizationService.GetRequiredString(ResourceKeys.InspectorFontPreviewTextureTitle),
            DefaultMaterialTitle: _localizationService.GetRequiredString(ResourceKeys.InspectorFontPreviewDefaultMaterialTitle),
            TextureReferenceText: BuildReferenceText(font.Texture),
            DefaultMaterialReferenceText: BuildReferenceText(font.DefaultMaterial),
            CharactersTitle: _localizationService.GetRequiredString(ResourceKeys.InspectorFontPreviewCharactersTitle),
            EmptyCharactersText: _localizationService.GetRequiredString(ResourceKeys.InspectorFontPreviewEmptyCharactersText),
            TruncatedText: font.IsCharacterListTruncated
                ? _localizationService.GetRequiredString(ResourceKeys.InspectorFontPreviewTruncatedText)
                : string.Empty,
            CharacterRows: font.Characters.Select(BuildCharacterRow).ToArray());
    }

    private FontCharacterRowDisplayState BuildCharacterRow(AssetPreviewFontCharacterDto character)
    {
        return new FontCharacterRowDisplayState(
            CodePointText: FormatCodePoint(character.CodePoint),
            GlyphText: FormatGlyph(character.DisplayText),
            UvText: BuildRectText(
                ResourceKeys.InspectorFontPreviewCharacterUvFormat,
                character.UvX,
                character.UvY,
                character.UvWidth,
                character.UvHeight),
            VertexText: BuildRectText(
                ResourceKeys.InspectorFontPreviewCharacterVertexFormat,
                character.VertexX,
                character.VertexY,
                character.VertexWidth,
                character.VertexHeight),
            AdvanceText: character.Advance.HasValue
                ? _localizationService.GetFormattedString(
                    ResourceKeys.InspectorFontPreviewCharacterAdvanceFormat,
                    FormatNumber(character.Advance))
                : string.Empty,
            FlippedText: character.IsFlipped.HasValue
                ? _localizationService.GetFormattedString(
                    ResourceKeys.InspectorFontPreviewCharacterFlippedFormat,
                    FormatBoolean(character.IsFlipped))
                : string.Empty);
    }

    private string BuildReferenceText(AssetPreviewFontReferenceDto? reference)
    {
        if (reference is null)
        {
            return _localizationService.GetRequiredString(ResourceKeys.InspectorFontPreviewReferenceMissingText);
        }

        var name = string.IsNullOrWhiteSpace(reference.Name)
            ? reference.SerializedFileName ?? UnknownValue
            : reference.Name.Trim();
        return reference.IsExternal
            ? _localizationService.GetFormattedString(
                ResourceKeys.InspectorFontPreviewReferenceExternalFormat,
                name,
                reference.PathId.ToString(CultureInfo.CurrentCulture))
            : _localizationService.GetFormattedString(
                ResourceKeys.InspectorFontPreviewReferenceInternalFormat,
                name,
                reference.PathId.ToString(CultureInfo.CurrentCulture));
    }

    private string BuildRectText(
        string resourceKey,
        double? x,
        double? y,
        double? width,
        double? height)
    {
        return x.HasValue && y.HasValue && width.HasValue && height.HasValue
            ? _localizationService.GetFormattedString(
                resourceKey,
                FormatNumber(x),
                FormatNumber(y),
                FormatNumber(width),
                FormatNumber(height))
            : string.Empty;
    }

    private string FormatCodePoint(long? codePoint)
    {
        return codePoint.HasValue && codePoint.Value >= 0
            ? _localizationService.GetFormattedString(
                ResourceKeys.InspectorFontPreviewCharacterCodePointFormat,
                codePoint.Value.ToString("X4", CultureInfo.InvariantCulture))
            : UnknownValue;
    }

    private static string FormatGlyph(string? glyph)
    {
        return string.IsNullOrWhiteSpace(glyph)
            ? string.Empty
            : glyph.Trim();
    }

    private string FormatBoolean(bool? value)
    {
        if (!value.HasValue)
        {
            return UnknownValue;
        }

        return _localizationService.GetRequiredString(value.Value
            ? ResourceKeys.InspectorBooleanTrue
            : ResourceKeys.InspectorBooleanFalse);
    }

    private static string ResolveAssetName(AssetPreviewFontDto font, string assetName)
    {
        if (!string.IsNullOrWhiteSpace(font.FontName))
        {
            return font.FontName.Trim();
        }

        return string.IsNullOrWhiteSpace(assetName)
            ? string.Empty
            : assetName.Trim();
    }

    private static string FormatInteger(int? value)
    {
        return value.HasValue
            ? value.Value.ToString(CultureInfo.CurrentCulture)
            : UnknownValue;
    }

    private static string FormatNumber(double? value)
    {
        return value.HasValue
            ? value.Value.ToString("0.###", CultureInfo.CurrentCulture)
            : UnknownValue;
    }
}

public sealed record FontPreviewDisplayState(
    string AssetNameText,
    string SummaryText,
    string MetricsText,
    string FontDataSizeText,
    string TextureTitle,
    string DefaultMaterialTitle,
    string TextureReferenceText,
    string DefaultMaterialReferenceText,
    string CharactersTitle,
    string EmptyCharactersText,
    string TruncatedText,
    IReadOnlyList<FontCharacterRowDisplayState> CharacterRows);

public sealed record FontCharacterRowDisplayState(
    string CodePointText,
    string GlyphText,
    string UvText,
    string VertexText,
    string AdvanceText,
    string FlippedText);
