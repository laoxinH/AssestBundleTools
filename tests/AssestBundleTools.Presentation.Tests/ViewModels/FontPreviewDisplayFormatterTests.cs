using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class FontPreviewDisplayFormatterTests
{
    [Fact]
    public void BuildDisplayState_ShouldFormatReferencesMetricsAndCharacterRows()
    {
        var formatter = new FontPreviewDisplayFormatter(new TestLocalizationService());
        var font = new AssetPreviewFontDto(
            SchemaVersion: 1,
            FontName: "LiberationSans SDF",
            FontSize: 18,
            LineSpacing: 20.5,
            Ascent: 17,
            Descent: -4,
            DefaultSize: 16,
            AsciiStartOffset: 32,
            Tracking: 1.25,
            CharacterSpacing: 2,
            CharacterPadding: 1,
            CharacterCount: 2,
            FontDataSizeInBytes: 512,
            Texture: new AssetPreviewFontReferenceDto(
                FileId: 2,
                PathId: 300,
                Name: "Atlas",
                SerializedFileName: "textures.assets",
                ExternalReferencePath: "archive:/textures/textures.assets",
                IsExternal: true),
            DefaultMaterial: new AssetPreviewFontReferenceDto(
                FileId: 0,
                PathId: 301,
                Name: "Material",
                SerializedFileName: "fonts.assets",
                ExternalReferencePath: null,
                IsExternal: false),
            Characters:
            [
                new AssetPreviewFontCharacterDto(
                    CodePoint: 65,
                    DisplayText: "A",
                    UvX: 0.1,
                    UvY: 0.2,
                    UvWidth: 0.05,
                    UvHeight: 0.06,
                    VertexX: -1,
                    VertexY: -2,
                    VertexWidth: 8,
                    VertexHeight: 9,
                    Advance: 8.5,
                    IsFlipped: false)
            ],
            IsCharacterListTruncated: true);

        var state = formatter.BuildDisplayState(font, "FallbackName");

        Assert.Equal("LiberationSans SDF", state.AssetNameText);
        Assert.Equal(ResourceKeys.InspectorFontPreviewSummaryFormat + ":2|16|20.5", state.SummaryText);
        Assert.Equal(ResourceKeys.InspectorFontPreviewMetricsFormat + ":17|-4|1.25|2|1", state.MetricsText);
        Assert.Equal(
            ResourceKeys.InspectorFontPreviewDataSizeFormat + ":" +
            ResourceKeys.CommonByteSizeDetailedBytesFormat + ":512",
            state.FontDataSizeText);
        Assert.Equal(ResourceKeys.InspectorFontPreviewTextureTitle, state.TextureTitle);
        Assert.Equal(ResourceKeys.InspectorFontPreviewDefaultMaterialTitle, state.DefaultMaterialTitle);
        Assert.Equal(ResourceKeys.InspectorFontPreviewReferenceExternalFormat + ":Atlas|300", state.TextureReferenceText);
        Assert.Equal(ResourceKeys.InspectorFontPreviewReferenceInternalFormat + ":Material|301", state.DefaultMaterialReferenceText);
        Assert.Equal(ResourceKeys.InspectorFontPreviewCharactersTitle, state.CharactersTitle);
        Assert.Equal(ResourceKeys.InspectorFontPreviewEmptyCharactersText, state.EmptyCharactersText);
        Assert.Equal(ResourceKeys.InspectorFontPreviewTruncatedText, state.TruncatedText);

        var row = Assert.Single(state.CharacterRows);
        Assert.Equal(ResourceKeys.InspectorFontPreviewCharacterCodePointFormat + ":0041", row.CodePointText);
        Assert.Equal("A", row.GlyphText);
        Assert.Equal(ResourceKeys.InspectorFontPreviewCharacterUvFormat + ":0.1|0.2|0.05|0.06", row.UvText);
        Assert.Equal(ResourceKeys.InspectorFontPreviewCharacterVertexFormat + ":-1|-2|8|9", row.VertexText);
        Assert.Equal(ResourceKeys.InspectorFontPreviewCharacterAdvanceFormat + ":8.5", row.AdvanceText);
        Assert.Equal(
            ResourceKeys.InspectorFontPreviewCharacterFlippedFormat + ":" + ResourceKeys.InspectorBooleanFalse,
            row.FlippedText);
    }
}
