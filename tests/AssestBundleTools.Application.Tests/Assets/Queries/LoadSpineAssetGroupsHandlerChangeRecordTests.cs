using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class LoadSpineAssetGroupsHandlerTests
{
    public static async Task HandleAsync_ShouldExposeUndonePartReplacementHistory_WhenComponentIsNotModified()
    {
        var appliedAtUtc = new DateTimeOffset(2026, 6, 4, 10, 0, 0, TimeSpan.Zero);
        var replacementSourcePath = @"C:\Imports\Hero.png";
        var workspaceId = new WorkspaceId("workspace-a");
        var spineBundleId = new BundleId("bundle.spine");
        var spineAssets = CreateAssetList(
            workspaceId,
            spineBundleId,
            "spine.ab",
            CreateItem(spineBundleId, "skeleton-hero", "Assets/Hero/Hero.skel", AssetType.TextAsset),
            CreateItem(spineBundleId, "atlas-hero", "Assets/Hero/Hero.atlas", AssetType.TextAsset),
            CreateItem(
                spineBundleId,
                "texture-hero",
                "Assets/Hero/Hero.png",
                AssetType.Texture2D,
                isModified: false));
        var details = new[]
        {
            CreateDetails(
                workspaceId,
                spineBundleId,
                "texture-hero",
                "Assets/Hero/Hero.png",
                AssetType.Texture2D,
                nameof(AssetType.Texture2D),
                dependencies: Array.Empty<AssetDependencyDto>(),
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
                ],
                isModified: false)
        };
        var handler = new LoadSpineAssetGroupsHandler(new TestWorkspaceSessionRegistry(
            workspaceId,
            [spineAssets],
            details));

        var result = await handler.HandleAsync(new LoadSpineAssetGroupsQuery(
            workspaceId,
            spineBundleId));

        TestAssert.True(result.IsSuccess, "Spine group query should succeed after the only component replacement has been undone.");
        var group = TestAssert.Single(result.Value!.Items);
        var texturePage = TestAssert.Single(group.TexturePageAssets);
        TestAssert.Equal(
            null,
            texturePage.FileReplacementSourcePath,
            "Undo should leave no current file replacement source on the Spine component.");
        var changeRecord = TestAssert.Single(texturePage.ChangeRecords ?? Array.Empty<AssetChangeRecordDto>());
        TestAssert.Equal(
            replacementSourcePath,
            changeRecord.SourceFilePath,
            "Undo should not remove the component replacement history from the Spine group projection.");
        TestAssert.False(
            changeRecord.IsEffectiveReplacement,
            "The undone component replacement should be retained as a non-effective history record.");
    }
}
