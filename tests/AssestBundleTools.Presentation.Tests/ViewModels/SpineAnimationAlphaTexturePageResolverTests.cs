using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class SpineAnimationAlphaTexturePageResolverTests
{
    [Fact]
    public void Resolve_ShouldMatchCommonAlphaCompanionSuffix()
    {
        var colorPart = CreateTexturePart("color", "body.png", "Assets/spine/body.png");
        var alphaPart = CreateTexturePart("alpha", "body_alpha.png", "Assets/spine/body_alpha.png");
        var details = CreateDetails(colorPart, alphaPart);
        var atlasPage = new SpineAtlasPageDto(
            "body.png",
            Width: 64,
            Height: 64,
            Regions: Array.Empty<SpineAtlasRegionDto>(),
            colorPart);

        var result = SpineAnimationAlphaTexturePageResolver.Resolve(details, atlasPage);

        Assert.Same(alphaPart, result);
    }

    [Theory]
    [InlineData("body[alpha].png")]
    [InlineData("body [alpha].png")]
    public void Resolve_ShouldMatchBracketedAlphaCompanionSuffix(string alphaTextureName)
    {
        var colorPart = CreateTexturePart("color", "body.png", "Assets/spine/body.png");
        var alphaPart = CreateTexturePart("alpha", alphaTextureName, string.Concat("Assets/spine/", alphaTextureName));
        var details = CreateDetails(colorPart, alphaPart);
        var atlasPage = new SpineAtlasPageDto(
            "body.png",
            Width: 64,
            Height: 64,
            Regions: Array.Empty<SpineAtlasRegionDto>(),
            colorPart);

        var result = SpineAnimationAlphaTexturePageResolver.Resolve(details, atlasPage);

        Assert.Same(alphaPart, result);
    }

    [Fact]
    public void Resolve_ShouldNotReturnColorPageItself()
    {
        var colorPart = CreateTexturePart("color", "body.png", "Assets/spine/body.png");
        var details = CreateDetails(colorPart);
        var atlasPage = new SpineAtlasPageDto(
            "body.png",
            Width: 64,
            Height: 64,
            Regions: Array.Empty<SpineAtlasRegionDto>(),
            colorPart);

        var result = SpineAnimationAlphaTexturePageResolver.Resolve(details, atlasPage);

        Assert.Null(result);
    }

    private static SpineAssetGroupDetailsDto CreateDetails(params SpineAssetGroupPartDto[] textureParts)
    {
        var group = new SpineAssetGroupDto(
            "group",
            new BundleId("bundle"),
            "hero",
            "assets/spine/hero",
            SkeletonAssets: Array.Empty<SpineAssetGroupPartDto>(),
            AtlasAssets: Array.Empty<SpineAssetGroupPartDto>(),
            TexturePageAssets: textureParts,
            Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>());
        return new SpineAssetGroupDetailsDto(
            group,
            SkeletonSummaries: Array.Empty<SpineSkeletonSummaryDto>(),
            AtlasPages: Array.Empty<SpineAtlasPageDto>(),
            Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>());
    }

    private static SpineAssetGroupPartDto CreateTexturePart(
        string assetId,
        string name,
        string assetPath) =>
        new(
            SpineAssetGroupPartRole.TexturePage,
            new BundleId("bundle"),
            new AssetId(assetId),
            "bundle.ab",
            name,
            assetPath,
            AssetType.Texture2D,
            nameof(AssetType.Texture2D));
}
