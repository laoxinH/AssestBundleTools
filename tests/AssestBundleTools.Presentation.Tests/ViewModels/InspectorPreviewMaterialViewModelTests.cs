using System.Runtime.CompilerServices;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
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
        var shaderTarget = new ResolvedAssetNavigationTarget(
            new AssetId("shader-asset"),
            new BundleId("bundle-material"),
            88,
            "Particles/Additive",
            "Shader");
        var mainTextureTarget = new ResolvedAssetNavigationTarget(
            new AssetId("main"),
            new BundleId("bundle-material"),
            100,
            "HeroTexture",
            "Texture2D");
        var viewModel = new InspectorPreviewMaterialViewModel();
        var displayState = new MaterialPreviewDisplayState(
            AssetNameText: "paopao_01",
            ShaderNameText: "Particles/Additive",
            ShaderReferenceText: "materials.assets",
            ShaderResolutionText: "Resolved Shader: Particles/Additive · Shader",
            ResolvedShaderAssetId: "shader-asset",
            PropertyCountText: "2 textures",
            MainTexturePreviewTitleText: "Main Texture",
            MainTexturePreviewLimitText: "Reference only",
            MainTextureTint: new MaterialPreviewTintDisplayState(
                Text: "Tint _Color: RGBA 0.25, 0.5, 0.75, 1",
                R: 0.25,
                G: 0.5,
                B: 0.75,
                A: 1),
            SemanticSectionTitle: "Material hints",
            SemanticBadges:
            [
                new MaterialPreviewSemanticBadgeDisplayState("Main texture detected"),
                new MaterialPreviewSemanticBadgeDisplayState("Transparent / blend candidate")
            ],
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
                    TransformText: "Scale 1, 1 · Offset 0, 0",
                    Target: mainTextureTarget,
                    OpenButtonText: "Open asset",
                    OpenButtonToolTipText: "Open resolved asset details")
            ],
            ColorRows: Array.Empty<MaterialPreviewColorRowDisplayState>(),
            NumberRows: Array.Empty<MaterialPreviewNumberRowDisplayState>(),
            Appearance: new MaterialPreviewAppearanceDisplayState(
                TitleText: "Simplified sample",
                NoteText: "Approximation only",
                BaseColorText: "Base color: RGBA 0.25, 0.5, 0.75, 1",
                AlphaText: "Alpha: 1",
                MetallicText: "Metallic: 0.6",
                SmoothnessText: "Smoothness: 0.8",
                R: 0.25,
                G: 0.5,
                B: 0.75,
                A: 1,
                Metallic: 0.6,
                Smoothness: 0.8,
                EmissionStrength: 0.5),
            TextureReferenceSummaryText: "1/2 texture references resolved",
            ShaderTarget: shaderTarget,
            OpenResolvedAssetButtonText: "Open asset",
            OpenResolvedAssetButtonToolTipText: "Open resolved asset details");

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
        Assert.Equal("Resolved Shader: Particles/Additive · Shader", viewModel.ShaderResolutionText);
        Assert.Equal("shader-asset", viewModel.ResolvedShaderAssetId);
        Assert.Same(shaderTarget, viewModel.ShaderTarget);
        Assert.Same(mainTextureTarget, viewModel.MainTexturePreviewTarget);
        Assert.Equal("Open asset", viewModel.OpenResolvedAssetButtonText);
        Assert.Equal("Simplified sample", viewModel.Appearance.TitleText);
        Assert.Equal("Base color: RGBA 0.25, 0.5, 0.75, 1", viewModel.Appearance.BaseColorText);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, viewModel.Appearance.Visibility);
        Assert.Equal("1/2 texture references resolved", viewModel.TextureReferenceSummaryText);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, viewModel.TextureReferenceSummaryVisibility);
        Assert.Equal("Material hints", viewModel.SemanticSectionTitle);
        Assert.Equal(
            ["Main texture detected", "Transparent / blend candidate"],
            viewModel.SemanticBadges.Select(static badge => badge.Text));
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, viewModel.MainTexturePreviewVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, viewModel.MainTexturePreviewTransformVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, viewModel.MainTexturePreviewTintTextVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, viewModel.MainTexturePreviewTintVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, viewModel.MainTexturePreviewLimitVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, viewModel.ShaderResolutionVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, viewModel.ShaderOpenButtonVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, viewModel.MainTexturePreviewOpenButtonVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, viewModel.SemanticHintsVisibility);
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
