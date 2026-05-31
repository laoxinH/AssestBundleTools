using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class SpineAssetGroupEditDraftViewModelTests
{
    [Fact]
    public void ApplyDirectoryMatches_ShouldMatchByPartRoleAndExtension()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"abt-spine-edit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        try
        {
            var skeletonPath = Path.Combine(directoryPath, "hero.skel.bytes");
            var atlasPath = Path.Combine(directoryPath, "hero.atlas");
            var texturePath = Path.Combine(directoryPath, "hero.png");
            File.WriteAllText(skeletonPath, "binary-skeleton");
            File.WriteAllText(atlasPath, "atlas");
            File.WriteAllText(texturePath, "png");

            var draft = new SpineAssetGroupEditDraftViewModel(
                CreateGroup(),
                new TestLocalizationService());

            draft.ApplyDirectoryMatches(directoryPath);

            Assert.Equal(
                skeletonPath,
                Assert.Single(draft.Parts, static part => part.Part.Role == SpineAssetGroupPartRole.Skeleton)
                    .SelectedSourceFilePath);
            Assert.Equal(
                atlasPath,
                Assert.Single(draft.Parts, static part => part.Part.Role == SpineAssetGroupPartRole.Atlas)
                    .SelectedSourceFilePath);
            Assert.Equal(
                texturePath,
                Assert.Single(draft.Parts, static part => part.Part.Role == SpineAssetGroupPartRole.TexturePage)
                    .SelectedSourceFilePath);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    private static SpineAssetGroupDto CreateGroup()
    {
        var bundleId = new BundleId("bundle-spine");
        return new SpineAssetGroupDto(
            GroupId: "spine-hero",
            BundleId: bundleId,
            DisplayName: "hero",
            GroupKey: "hero",
            SkeletonAssets:
            [
                CreatePart(SpineAssetGroupPartRole.Skeleton, bundleId, "skel", "hero.skel.bytes", AssetType.TextAsset)
            ],
            AtlasAssets:
            [
                CreatePart(SpineAssetGroupPartRole.Atlas, bundleId, "atlas", "hero.atlas", AssetType.TextAsset)
            ],
            TexturePageAssets:
            [
                CreatePart(SpineAssetGroupPartRole.TexturePage, bundleId, "texture", "hero.png", AssetType.Texture2D)
            ],
            Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>());
    }

    private static SpineAssetGroupPartDto CreatePart(
        SpineAssetGroupPartRole role,
        BundleId bundleId,
        string assetId,
        string name,
        AssetType assetType) =>
        new(
            role,
            bundleId,
            new AssetId(assetId),
            "bundle-spine",
            name,
            $"Assets/Spine/{name}",
            assetType,
            assetType.ToString());
}
