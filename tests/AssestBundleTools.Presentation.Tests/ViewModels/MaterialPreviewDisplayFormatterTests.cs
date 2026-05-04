using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
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
}
