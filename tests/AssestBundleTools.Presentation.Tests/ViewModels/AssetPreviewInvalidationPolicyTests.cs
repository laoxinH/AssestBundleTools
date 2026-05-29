using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class AssetPreviewInvalidationPolicyTests
{
    [Fact]
    public void HasEffectivePreviewProjectionChanged_ShouldDetectAssociatedReplacementEffectivenessChange()
    {
        var workspaceId = new WorkspaceId("workspace-preview-invalidation");
        var bundleId = new BundleId("bundle-ui");
        var spriteAssetId = new AssetId("asset-sprite");
        var appliedAtUtc = new DateTimeOffset(2026, 5, 29, 12, 0, 0, TimeSpan.Zero);
        var previousDetails = CreateSpriteDetailsWithAssociatedRecord(
            workspaceId,
            bundleId,
            spriteAssetId,
            appliedAtUtc,
            isEffectiveReplacement: true);
        var currentDetails = CreateSpriteDetailsWithAssociatedRecord(
            workspaceId,
            bundleId,
            spriteAssetId,
            appliedAtUtc,
            isEffectiveReplacement: false);

        var changed = AssetPreviewInvalidationPolicy.HasEffectivePreviewProjectionChanged(
            previousDetails,
            currentDetails);

        Assert.True(changed);
    }

    private static AssetDetailsDto CreateSpriteDetailsWithAssociatedRecord(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId spriteAssetId,
        DateTimeOffset appliedAtUtc,
        bool isEffectiveReplacement) =>
        BundleExplorerPreviewTestSupport.CreateAssetDetails(
            workspaceId,
            bundleId,
            spriteAssetId) with
        {
            AssetType = AssetType.Sprite,
            ChangeRecords =
            [
                new AssetChangeRecordDto(
                    AssetChangeRecordKind.AssociatedFileReplacement,
                    CreatedAtUtc: appliedAtUtc,
                    AppliedAtUtc: appliedAtUtc,
                    SourceFileName: "texture.png",
                    SourceFilePath: @"D:\Imports\texture.png",
                    OperationId: "replace-texture",
                    Sequence: 1,
                    IsEffectiveReplacement: isEffectiveReplacement,
                    TargetBundleId: bundleId.Value,
                    TargetAssetId: "asset-texture",
                    TargetAssetName: "HeroTexture",
                    TargetAssetType: AssetType.Texture2D.ToString(),
                    IsRelatedOperation: true)
            ]
        };
}
