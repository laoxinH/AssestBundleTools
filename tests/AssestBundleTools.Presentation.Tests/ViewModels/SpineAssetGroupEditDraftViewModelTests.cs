using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class SpineAssetGroupEditDraftViewModelTests
{
    [Fact]
    public void Constructor_ShouldExposeAppliedReplacementSourceWithoutDraftChange()
    {
        var appliedSourcePath = @"C:\Imports\hero.png";
        var bundleId = new BundleId("bundle-spine");
        var draft = new SpineAssetGroupEditDraftViewModel(
            new SpineAssetGroupDto(
                GroupId: "spine-hero",
                BundleId: bundleId,
                DisplayName: "hero",
                GroupKey: "hero",
                SkeletonAssets: Array.Empty<SpineAssetGroupPartDto>(),
                AtlasAssets: Array.Empty<SpineAssetGroupPartDto>(),
                TexturePageAssets:
                [
                    CreatePart(
                        SpineAssetGroupPartRole.TexturePage,
                        bundleId,
                        "texture",
                        "hero.png",
                        AssetType.Texture2D,
                        appliedSourcePath)
                ],
                Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>()),
            new TestLocalizationService());

        var part = Assert.Single(draft.Parts);

        Assert.False(draft.HasChanges);
        Assert.Null(part.SelectedSourceFilePath);
        Assert.Equal(appliedSourcePath, part.AppliedSourceFilePath);
        Assert.Equal(appliedSourcePath, part.SourceText);
    }

    [Fact]
    public void MarkSaved_ShouldPromoteSelectedSourceToAppliedSource()
    {
        var bundleId = new BundleId("bundle-spine");
        var draft = new SpineAssetGroupEditDraftViewModel(
            new SpineAssetGroupDto(
                GroupId: "spine-hero",
                BundleId: bundleId,
                DisplayName: "hero",
                GroupKey: "hero",
                SkeletonAssets: Array.Empty<SpineAssetGroupPartDto>(),
                AtlasAssets: Array.Empty<SpineAssetGroupPartDto>(),
                TexturePageAssets:
                [
                    CreatePart(
                        SpineAssetGroupPartRole.TexturePage,
                        bundleId,
                        "texture",
                        "hero.png",
                        AssetType.Texture2D)
                ],
                Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>()),
            new TestLocalizationService());
        var part = Assert.Single(draft.Parts);
        var selectedSourcePath = @"D:\Replacement\hero.png";

        Assert.True(draft.ApplyPartSource(part, selectedSourcePath));
        draft.MarkSaved();

        Assert.False(draft.HasChanges);
        Assert.Null(part.SelectedSourceFilePath);
        Assert.Equal(selectedSourcePath, part.AppliedSourceFilePath);
        Assert.Equal(selectedSourcePath, part.SourceText);
    }

    [Fact]
    public void ApplyMatches_ShouldApplyApplicationResolvedSources()
    {
        var skeletonPath = @"D:\Replacement\hero.skel";
        var atlasPath = @"D:\Replacement\hero.atlas";
        var texturePath = @"D:\Replacement\hero.png";
        var draft = new SpineAssetGroupEditDraftViewModel(CreateGroup(), new TestLocalizationService());

        Assert.True(draft.ApplyMatches(
        [
            new SpineReplacementSourceMatchDto(new AssetId("skel"), skeletonPath),
            new SpineReplacementSourceMatchDto(new AssetId("atlas"), atlasPath),
            new SpineReplacementSourceMatchDto(new AssetId("texture"), texturePath)
        ]));
        Assert.Equal(3, draft.ChangedParts.Count);
        Assert.Equal(skeletonPath, Assert.Single(draft.Parts, static part => part.Part.Role == SpineAssetGroupPartRole.Skeleton).SelectedSourceFilePath);
        Assert.Equal(atlasPath, Assert.Single(draft.Parts, static part => part.Part.Role == SpineAssetGroupPartRole.Atlas).SelectedSourceFilePath);
        Assert.Equal(texturePath, Assert.Single(draft.Parts, static part => part.Part.Role == SpineAssetGroupPartRole.TexturePage).SelectedSourceFilePath);
    }

    [Fact]
    public void ApplyMatches_ShouldNotCreateChanges_WhenNoSourcesMatched()
    {
        var draft = new SpineAssetGroupEditDraftViewModel(CreateGroup(), new TestLocalizationService());

        Assert.False(draft.ApplyMatches([]));
        Assert.False(draft.HasChanges);
        Assert.Empty(draft.ChangedParts);
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
        AssetType assetType,
        string? fileReplacementSourcePath = null) =>
        new(
            role,
            bundleId,
            new AssetId(assetId),
            "bundle-spine",
            name,
            $"Assets/Spine/{name}",
            assetType,
            assetType.ToString(),
            FileReplacementSourcePath: fileReplacementSourcePath);
}
