using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class InspectorPreviewFontViewModelTests
{
    [Fact]
    public void ApplyAndClear_ShouldProjectDisplayStateAndVisibility()
    {
        var viewModel = new InspectorPreviewFontViewModel();
        var font = new AssetPreviewFontDto(
            SchemaVersion: 1,
            FontName: "HeroFont",
            FontSize: null,
            LineSpacing: null,
            Ascent: null,
            Descent: null,
            DefaultSize: null,
            AsciiStartOffset: null,
            Tracking: null,
            CharacterSpacing: null,
            CharacterPadding: null,
            CharacterCount: 1,
            FontDataSizeInBytes: null,
            Texture: null,
            DefaultMaterial: null,
            Characters: Array.Empty<AssetPreviewFontCharacterDto>());
        var displayState = new FontPreviewDisplayState(
            AssetNameText: "HeroFont",
            SummaryText: "1 character",
            MetricsText: "Metrics",
            FontDataSizeText: "512 bytes",
            TextureTitle: "Texture",
            DefaultMaterialTitle: "Default Material",
            TextureReferenceText: "Atlas",
            DefaultMaterialReferenceText: "Material",
            CharactersTitle: "Character Rects",
            EmptyCharactersText: "No character rects",
            TruncatedText: "Truncated",
            CharacterRows:
            [
                new FontCharacterRowDisplayState(
                    CodePointText: "U+0041",
                    GlyphText: "A",
                    UvText: "UV",
                    VertexText: "Vertex",
                    AdvanceText: "Advance 8",
                    FlippedText: "Flipped False")
            ]);

        viewModel.Apply(font, displayState);

        Assert.True(viewModel.HasFont);
        Assert.Equal(Visibility.Visible, viewModel.Visibility);
        Assert.Equal("HeroFont", viewModel.AssetNameText);
        Assert.Equal("512 bytes", viewModel.FontDataSizeText);
        Assert.Equal(Visibility.Visible, viewModel.FontDataSizeVisibility);
        Assert.Equal(Visibility.Visible, viewModel.CharacterRowsVisibility);
        Assert.Equal(Visibility.Collapsed, viewModel.CharacterRowsEmptyVisibility);
        Assert.Equal(Visibility.Visible, viewModel.TruncatedTextVisibility);
        var row = Assert.Single(viewModel.CharacterRows);
        Assert.Equal("A", row.GlyphText);
        Assert.Equal(Visibility.Visible, row.GlyphVisibility);

        viewModel.Clear();

        Assert.False(viewModel.HasFont);
        Assert.Equal(Visibility.Collapsed, viewModel.Visibility);
        Assert.Equal(string.Empty, viewModel.AssetNameText);
        Assert.Equal(Visibility.Collapsed, viewModel.FontDataSizeVisibility);
        Assert.Equal(Visibility.Collapsed, viewModel.CharacterRowsVisibility);
        Assert.Equal(Visibility.Visible, viewModel.CharacterRowsEmptyVisibility);
        Assert.Equal(Visibility.Collapsed, viewModel.TruncatedTextVisibility);
    }
}
