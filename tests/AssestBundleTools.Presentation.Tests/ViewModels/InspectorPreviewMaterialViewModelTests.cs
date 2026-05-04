using System.Runtime.CompilerServices;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class InspectorPreviewMaterialViewModelTests
{
    [Fact]
    public void Apply_ShouldPromoteMainTextureThumbnail_ToPlanePreview()
    {
        var mainTextureImageSource = CreateImageSourceStub();
        var viewModel = new InspectorPreviewMaterialViewModel();
        var displayState = new MaterialPreviewDisplayState(
            AssetNameText: "paopao_01",
            ShaderNameText: "Particles/Additive",
            ShaderReferenceText: "materials.assets",
            PropertyCountText: "2 textures",
            MainTexturePreviewTitleText: "Main Texture",
            MainTexturePreviewLimitText: "Reference only",
            MainTextureTint: new MaterialPreviewTintDisplayState(
                Text: "Tint _Color: RGBA 0.25, 0.5, 0.75, 1",
                R: 0.25,
                G: 0.5,
                B: 0.75,
                A: 1),
            TextureSectionTitle: "Textures",
            ColorSectionTitle: "Colors",
            NumberSectionTitle: "Numbers",
            EmptySectionText: "Empty",
            TruncatedText: string.Empty,
            TextureRows:
            [
                new MaterialPreviewTextureRowDisplayState(
                    PropertyName: "_DisturbTex",
                    TargetText: "Disturb",
                    ResolutionText: "Resolved disturb",
                    ResolvedTextureAssetId: "disturb",
                    IsMainTextureCandidate: false,
                    TransformText: string.Empty),
                new MaterialPreviewTextureRowDisplayState(
                    PropertyName: "_MainTex",
                    TargetText: "Main",
                    ResolutionText: "Resolved main",
                    ResolvedTextureAssetId: "main",
                    IsMainTextureCandidate: true,
                    TransformText: "Scale 1, 1 · Offset 0, 0")
            ],
            ColorRows: Array.Empty<MaterialPreviewColorRowDisplayState>(),
            NumberRows: Array.Empty<MaterialPreviewNumberRowDisplayState>());

        viewModel.Apply(
            CreateEmptyMaterial(),
            displayState,
            new Dictionary<string, ImageSource>(StringComparer.Ordinal)
            {
                ["disturb"] = CreateImageSourceStub(),
                ["main"] = mainTextureImageSource
            });

        Assert.Same(mainTextureImageSource, viewModel.MainTexturePreviewImageSource);
        Assert.Equal("Main Texture", viewModel.MainTexturePreviewTitleText);
        Assert.Equal("_MainTex", viewModel.MainTexturePreviewNameText);
        Assert.Equal("Resolved main", viewModel.MainTexturePreviewResolutionText);
        Assert.Equal("Scale 1, 1 · Offset 0, 0", viewModel.MainTexturePreviewTransformText);
        Assert.Equal("Tint _Color: RGBA 0.25, 0.5, 0.75, 1", viewModel.MainTexturePreviewTintText);
        Assert.Equal("Reference only", viewModel.MainTexturePreviewLimitText);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, viewModel.MainTexturePreviewVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, viewModel.MainTexturePreviewTransformVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, viewModel.MainTexturePreviewTintTextVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, viewModel.MainTexturePreviewTintVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, viewModel.MainTexturePreviewLimitVisibility);
    }

    private static AssetPreviewMaterialDto CreateEmptyMaterial()
    {
        return new AssetPreviewMaterialDto(
            SchemaVersion: 1,
            Shader: null,
            TextureProperties: Array.Empty<AssetPreviewMaterialTexturePropertyDto>(),
            FloatProperties: Array.Empty<AssetPreviewMaterialFloatPropertyDto>(),
            IntegerProperties: Array.Empty<AssetPreviewMaterialIntegerPropertyDto>(),
            ColorProperties: Array.Empty<AssetPreviewMaterialColorPropertyDto>());
    }

    private static ImageSource CreateImageSourceStub()
    {
        return (BitmapImage)RuntimeHelpers.GetUninitializedObject(typeof(BitmapImage));
    }
}
