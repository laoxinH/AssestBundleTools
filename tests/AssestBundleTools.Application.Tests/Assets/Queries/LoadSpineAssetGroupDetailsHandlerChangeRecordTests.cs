using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class LoadSpineAssetGroupDetailsHandlerTests
{
    public static async Task HandleAsync_ShouldExposeUndonePartReplacementHistory_InGroupDetails()
    {
        var appliedAtUtc = new DateTimeOffset(2026, 6, 4, 10, 30, 0, TimeSpan.Zero);
        var replacementSourcePath = @"C:\Imports\Hero.png";
        var workspaceId = new WorkspaceId("workspace-a");
        var spineBundleId = new BundleId("bundle.spine");
        var skeletonAssetId = new AssetId("skeleton-hero");
        var atlasAssetId = new AssetId("atlas-hero");
        var textureAssetId = new AssetId("texture-hero");
        var spineAssets = CreateAssetList(
            workspaceId,
            spineBundleId,
            "spine.ab",
            CreateItem(spineBundleId, skeletonAssetId.Value, "Assets/Hero/Hero.skel", AssetType.TextAsset),
            CreateItem(spineBundleId, atlasAssetId.Value, "Assets/Hero/Hero.atlas", AssetType.TextAsset),
            CreateItem(spineBundleId, textureAssetId.Value, "Assets/Hero/Hero.png", AssetType.Texture2D));
        var textureDetails = CreateDetails(
            workspaceId,
            spineBundleId,
            textureAssetId.Value,
            "Assets/Hero/Hero.png",
            AssetType.Texture2D,
            changeRecords:
            [
                new AssetChangeRecordDto(
                    AssetChangeRecordKind.FileReplacement,
                    CreatedAtUtc: appliedAtUtc,
                    AppliedAtUtc: appliedAtUtc,
                    SourceFileName: "Hero.png",
                    SourceFilePath: replacementSourcePath,
                    OperationId: "replace-texture",
                    IsEffectiveReplacement: false)
            ]);
        var previewHandler = new TestAssetPreviewHandler(query =>
        {
            if (query.AssetId.Equals(atlasAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(query, """
                    hero.png
                    size: 1,1
                    dot
                      xy: 0, 0
                      size: 1, 1
                    """));
            }

            if (query.AssetId.Equals(textureAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateImagePreview(query, 1, 1));
            }

            return query.AssetId.Equals(skeletonAssetId)
                ? Result<AssetPreviewDto>.Success(CreateBinaryPreview(query, [1, 2, 3]))
                : Result<AssetPreviewDto>.Failure(ApplicationErrors.AssetNotFound(query.AssetId));
        });
        var handler = new LoadSpineAssetGroupDetailsHandler(
            new TestWorkspaceSessionRegistry(workspaceId, [spineAssets], [textureDetails]),
            previewHandler);

        var result = await handler.HandleAsync(CreateQuery(workspaceId, spineBundleId));

        TestAssert.True(result.IsSuccess, "Spine group details should load after the only component replacement has been undone.");
        var details = result.Value ?? throw new InvalidOperationException("Expected Spine group details.");
        var texturePage = TestAssert.Single(details.Group.TexturePageAssets);
        TestAssert.Equal(
            null,
            texturePage.FileReplacementSourcePath,
            "Undo should not project a current file replacement source on the Spine group part.");
        var changeRecord = TestAssert.Single(texturePage.ChangeRecords ?? Array.Empty<AssetChangeRecordDto>());
        TestAssert.Equal(
            replacementSourcePath,
            changeRecord.SourceFilePath,
            "Spine group details should retain undone component replacement history.");
        TestAssert.False(
            changeRecord.IsEffectiveReplacement,
            "The retained replacement history should be projected as non-effective.");
    }

    private static AssetDetailsDto CreateDetails(
        WorkspaceId workspaceId,
        BundleId bundleId,
        string assetId,
        string assetPath,
        AssetType assetType,
        IReadOnlyList<AssetChangeRecordDto>? changeRecords = null)
    {
        var assetName = assetPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
        return new AssetDetailsDto(
            WorkspaceId: workspaceId,
            BundleId: bundleId,
            AssetId: new AssetId(assetId),
            Name: assetName,
            AssetPath: assetPath,
            ContainerPath: assetPath,
            AssetType: assetType,
            SizeInBytes: 1,
            PreferredDisplaySizeInBytes: null,
            PathId: 0,
            IsModified: false,
            CanExtract: true,
            CanReplaceFromFile: true,
            CanReplaceFromWorkspace: true,
            WorkspaceReplacementSourceBundleDisplayName: null,
            WorkspaceReplacementSourceAssetName: null,
            WorkspaceReplacementSourceAssetType: null,
            FileReplacementSourceFileName: null,
            FileReplacementSourcePath: null,
            FileReplacementSourceAssetType: null,
            LastExtractedAtUtc: null,
            Dependencies: Array.Empty<AssetDependencyDto>(),
            AssetTypeDisplayName: assetType.ToString(),
            ChangeRecords: changeRecords);
    }
}
