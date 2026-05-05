using System.Runtime.CompilerServices;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using static AssestBundleTools.Presentation.Tests.ViewModels.BundleExplorerPreviewTestSupport;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class MaterialPreviewDisplayFormatterTests
{
    [Fact]
    public void BuildDisplayState_ShouldResolveShaderDependency_ByPathIdAndUnityTypeName()
    {
        var formatter = new MaterialPreviewDisplayFormatter(new TestLocalizationService());
        var shaderAssetId = new AssetId("asset-shader");
        var bundleId = new BundleId("bundle-material");
        var material = new AssetPreviewMaterialDto(
            SchemaVersion: 1,
            Shader: new AssetPreviewMaterialShaderDto(
                FileId: 0,
                PathId: 100,
                Name: "Particles/Additive",
                SerializedFileName: "materials.assets",
                ExternalReferencePath: null,
                IsExternal: false),
            TextureProperties: Array.Empty<AssetPreviewMaterialTexturePropertyDto>(),
            FloatProperties: Array.Empty<AssetPreviewMaterialFloatPropertyDto>(),
            IntegerProperties: Array.Empty<AssetPreviewMaterialIntegerPropertyDto>(),
            ColorProperties: Array.Empty<AssetPreviewMaterialColorPropertyDto>());
        var details = CreateAssetDetails(
            WorkspaceId.New(),
            bundleId,
            new AssetId("asset-material")) with
        {
            Name = "paopao_01",
            AssetType = AssetType.Material,
            AssetTypeDisplayName = AssetType.Material.ToString(),
            Dependencies =
            [
                new AssetDependencyDto(
                    shaderAssetId,
                    bundleId,
                    "Particles/Additive",
                    AssetType.Unknown,
                    PathId: 100,
                    AssetTypeDisplayName: "Shader")
            ]
        };

        var displayState = formatter.BuildDisplayState(material, details);

        Assert.Equal(
            ResourceKeys.InspectorMaterialPreviewShaderResolvedFormat + ":Particles/Additive|Shader",
            displayState.ShaderResolutionText);
        Assert.Equal(shaderAssetId.Value, displayState.ResolvedShaderAssetId);
        Assert.NotNull(displayState.ShaderTarget);
        Assert.Equal(shaderAssetId, displayState.ShaderTarget.AssetId);
        Assert.Equal(bundleId, displayState.ShaderTarget.BundleId);
        Assert.Equal(100, displayState.ShaderTarget.PathId);
        Assert.Equal("Particles/Additive", displayState.ShaderTarget.DisplayName);
        Assert.Equal("Shader", displayState.ShaderTarget.AssetTypeText);
        Assert.Equal(ResourceKeys.InspectorResolvedAssetOpenButton, displayState.OpenResolvedAssetButtonText);
    }

    [Fact]
    public void BuildDisplayState_ShouldSurfaceSemanticBadges_ForCommonMaterialProperties()
    {
        var formatter = new MaterialPreviewDisplayFormatter(new TestLocalizationService());
        var material = new AssetPreviewMaterialDto(
            SchemaVersion: 1,
            Shader: null,
            TextureProperties:
            [
                CreateTextureProperty("_MainTex", 10),
                CreateTextureProperty("_BumpMap", 20),
                CreateTextureProperty("_EmissionMap", 30),
                CreateTextureProperty("_DissolveTex", 40)
            ],
            FloatProperties:
            [
                new AssetPreviewMaterialFloatPropertyDto("_Cutoff", 0.5),
                new AssetPreviewMaterialFloatPropertyDto("_Metallic", 0.75),
                new AssetPreviewMaterialFloatPropertyDto("_Mode", 3)
            ],
            IntegerProperties: Array.Empty<AssetPreviewMaterialIntegerPropertyDto>(),
            ColorProperties: Array.Empty<AssetPreviewMaterialColorPropertyDto>());
        var details = CreateAssetDetails(
            WorkspaceId.New(),
            new BundleId("bundle-material"),
            new AssetId("asset-material")) with
        {
            Name = "paopao_01",
            AssetType = AssetType.Material,
            AssetTypeDisplayName = AssetType.Material.ToString()
        };

        var displayState = formatter.BuildDisplayState(material, details);

        Assert.Equal(
            ResourceKeys.InspectorMaterialPreviewSemanticHintsTitle,
            displayState.SemanticSectionTitle);
        Assert.Equal(
            [
                ResourceKeys.InspectorMaterialPreviewSemanticMainTextureDetectedText,
                ResourceKeys.InspectorMaterialPreviewSemanticNormalMapCandidateText,
                ResourceKeys.InspectorMaterialPreviewSemanticEmissionCandidateText,
                ResourceKeys.InspectorMaterialPreviewSemanticAlphaCutoffCandidateText,
                ResourceKeys.InspectorMaterialPreviewSemanticMetallicSmoothnessCandidateText,
                ResourceKeys.InspectorMaterialPreviewSemanticDissolveDistortionCandidateText,
                ResourceKeys.InspectorMaterialPreviewSemanticTransparentBlendCandidateText
            ],
            displayState.SemanticBadges.Select(static badge => badge.Text));
    }

    [Fact]
    public void BuildDisplayState_ShouldBuildAppearanceSample_FromCommonMaterialProperties()
    {
        var formatter = new MaterialPreviewDisplayFormatter(new TestLocalizationService());
        var material = new AssetPreviewMaterialDto(
            SchemaVersion: 1,
            Shader: null,
            TextureProperties: Array.Empty<AssetPreviewMaterialTexturePropertyDto>(),
            FloatProperties:
            [
                new AssetPreviewMaterialFloatPropertyDto("_Metallic", 0.8),
                new AssetPreviewMaterialFloatPropertyDto("_Roughness", 0.25)
            ],
            IntegerProperties: Array.Empty<AssetPreviewMaterialIntegerPropertyDto>(),
            ColorProperties:
            [
                new AssetPreviewMaterialColorPropertyDto("_BaseColor", 0.2, 0.4, 0.6, 0.5),
                new AssetPreviewMaterialColorPropertyDto("_EmissionColor", 0.9, 0.1, 0.1, 1)
            ]);
        var details = CreateAssetDetails(
            WorkspaceId.New(),
            new BundleId("bundle-material"),
            new AssetId("asset-material")) with
        {
            Name = "paopao_01",
            AssetType = AssetType.Material,
            AssetTypeDisplayName = AssetType.Material.ToString()
        };

        var displayState = formatter.BuildDisplayState(material, details);

        Assert.NotNull(displayState.Appearance);
        var appearance = displayState.Appearance;
        Assert.Equal(ResourceKeys.InspectorMaterialPreviewAppearanceTitle, appearance.TitleText);
        Assert.Equal(ResourceKeys.InspectorMaterialPreviewAppearanceNote, appearance.NoteText);
        Assert.Equal(
            ResourceKeys.InspectorMaterialPreviewAppearanceBaseColorFormat + ":" +
            ResourceKeys.InspectorMaterialPreviewColorValueFormat + ":0.2|0.4|0.6|0.5",
            appearance.BaseColorText);
        Assert.Equal(ResourceKeys.InspectorMaterialPreviewAppearanceAlphaFormat + ":0.5", appearance.AlphaText);
        Assert.Equal(ResourceKeys.InspectorMaterialPreviewAppearanceMetallicFormat + ":0.8", appearance.MetallicText);
        Assert.Equal(ResourceKeys.InspectorMaterialPreviewAppearanceSmoothnessFormat + ":0.75", appearance.SmoothnessText);
        Assert.Equal(0.2, appearance.R);
        Assert.Equal(0.4, appearance.G);
        Assert.Equal(0.6, appearance.B);
        Assert.Equal(0.5, appearance.A);
        Assert.Equal(0.8, appearance.Metallic);
        Assert.Equal(0.75, appearance.Smoothness);
        Assert.Equal(0.9, appearance.EmissionStrength);
    }

    [Fact]
    public void BuildDisplayState_ShouldSummarizeTextureReferenceResolution()
    {
        var formatter = new MaterialPreviewDisplayFormatter(new TestLocalizationService());
        var textureAssetId = new AssetId("texture-main");
        var bundleId = new BundleId("bundle-material");
        var material = new AssetPreviewMaterialDto(
            SchemaVersion: 1,
            Shader: null,
            TextureProperties:
            [
                CreateTextureProperty("_MainTex", 10),
                CreateTextureProperty("_ExternalTex", 20) with
                {
                    FileId = 1,
                    ExternalReferencePath = "archive:/CAB-ext/CAB-ext",
                    IsExternal = true
                },
                CreateTextureProperty("_InternalMissing", 30)
            ],
            FloatProperties: Array.Empty<AssetPreviewMaterialFloatPropertyDto>(),
            IntegerProperties: Array.Empty<AssetPreviewMaterialIntegerPropertyDto>(),
            ColorProperties: Array.Empty<AssetPreviewMaterialColorPropertyDto>());
        var details = CreateAssetDetails(
            WorkspaceId.New(),
            bundleId,
            new AssetId("asset-material")) with
        {
            Name = "paopao_01",
            AssetType = AssetType.Material,
            AssetTypeDisplayName = AssetType.Material.ToString(),
            Dependencies =
            [
                new AssetDependencyDto(
                    textureAssetId,
                    bundleId,
                    "MainTexture",
                    AssetType.Texture2D,
                    PathId: 10,
                    AssetTypeDisplayName: "Texture2D")
            ]
        };

        var displayState = formatter.BuildDisplayState(material, details);

        Assert.Equal(
            ResourceKeys.InspectorMaterialPreviewTextureReferenceSummaryFormat + ":1|3|1|1",
            displayState.TextureReferenceSummaryText);
    }

    [Fact]
    public void BuildDisplayState_ShouldAttachMaterialVisualDetails_ToInformationCards()
    {
        var formatter = new MaterialPreviewDisplayFormatter(new TestLocalizationService());
        var textureAssetId = new AssetId("texture-main");
        var bundleId = new BundleId("bundle-material");
        var thumbnailSource = CreateImageSourceStub();
        var material = new AssetPreviewMaterialDto(
            SchemaVersion: 1,
            Shader: null,
            TextureProperties:
            [
                CreateTextureProperty("_MainTex", 10)
            ],
            FloatProperties: Array.Empty<AssetPreviewMaterialFloatPropertyDto>(),
            IntegerProperties: Array.Empty<AssetPreviewMaterialIntegerPropertyDto>(),
            ColorProperties:
            [
                new AssetPreviewMaterialColorPropertyDto("_Color", 0.2, 0.4, 0.6, 0.5)
            ]);
        var details = CreateAssetDetails(
            WorkspaceId.New(),
            bundleId,
            new AssetId("asset-material")) with
        {
            Name = "paopao_01",
            AssetType = AssetType.Material,
            AssetTypeDisplayName = AssetType.Material.ToString(),
            Dependencies =
            [
                new AssetDependencyDto(
                    textureAssetId,
                    bundleId,
                    "MainTexture",
                    AssetType.Texture2D,
                    PathId: 10,
                    AssetTypeDisplayName: "Texture2D")
            ]
        };

        var displayState = formatter.BuildDisplayState(
            material,
            details,
            new Dictionary<string, ImageSource>(StringComparer.Ordinal)
            {
                [textureAssetId.Value] = thumbnailSource
            });
        Assert.NotNull(displayState.DetailCards);
        var detailCards = displayState.DetailCards;

        var appearanceCard = Assert.Single(
            detailCards,
            static card => card.Key == "material-appearance");
        Assert.NotNull(appearanceCard.VisualMaterialAppearance);
        Assert.Empty(appearanceCard.Rows);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, appearanceCard.MaterialAppearanceVisualVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, appearanceCard.CopyButtonVisibility);

        var texturesCard = Assert.Single(
            detailCards,
            static card => card.Key == "material-textures");
        Assert.Same(thumbnailSource, texturesCard.VisualImageSource);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, texturesCard.ImagePreviewVisualVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, texturesCard.ImagePreviewTintVisibility);
        var textureRow = Assert.Single(texturesCard.Rows);
        Assert.True(textureRow.ShowThumbnailSlot);
        Assert.Same(thumbnailSource, textureRow.ThumbnailImageSource);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, textureRow.ThumbnailVisibility);

        var colorsCard = Assert.Single(
            detailCards,
            static card => card.Key == "material-colors");
        var colorRow = Assert.Single(colorsCard.Rows);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, colorRow.ColorSwatchVisibility);
    }

    private static AssetPreviewMaterialTexturePropertyDto CreateTextureProperty(
        string name,
        long pathId)
    {
        return new AssetPreviewMaterialTexturePropertyDto(
            Name: name,
            FileId: 0,
            PathId: pathId,
            SerializedFileName: "materials.assets",
            ExternalReferencePath: null,
            IsExternal: false,
            ScaleX: null,
            ScaleY: null,
            OffsetX: null,
            OffsetY: null);
    }

    private static ImageSource CreateImageSourceStub()
    {
        return (BitmapImage)RuntimeHelpers.GetUninitializedObject(typeof(BitmapImage));
    }
}
