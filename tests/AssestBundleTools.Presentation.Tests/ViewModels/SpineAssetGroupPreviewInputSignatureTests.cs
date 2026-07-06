using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class SpineAssetGroupPreviewInputSignatureTests
{
    [Fact]
    public void Create_ShouldChangeWhenEffectiveTextEditRevisionChanges()
    {
        var groupTab = CreateGroupTab(CreateTextEditRecord(revision: 1));
        var previousSignature = SpineAssetGroupPreviewInputSignature.Create(groupTab);

        groupTab.ApplyOperationProjection(CreateGroup(CreateTextEditRecord(revision: 2)));
        var currentSignature = SpineAssetGroupPreviewInputSignature.Create(groupTab);

        Assert.NotEqual(previousSignature, currentSignature);
    }

    [Fact]
    public void Create_ShouldIgnoreNonEffectiveTextEditRecords()
    {
        var groupTab = CreateGroupTab(CreateTextEditRecord(revision: 1));
        var previousSignature = SpineAssetGroupPreviewInputSignature.Create(groupTab);

        groupTab.ApplyOperationProjection(CreateGroup(
            CreateTextEditRecord(revision: 1),
            CreateTextEditRecord(revision: 2, isEffective: false)));
        var currentSignature = SpineAssetGroupPreviewInputSignature.Create(groupTab);

        Assert.Equal(previousSignature, currentSignature);
    }

    private static SpineAssetGroupDocumentTabViewModel CreateGroupTab(params AssetChangeRecordDto[] changeRecords) =>
        new(
            CreateGroup(changeRecords),
            InspectorInformationTextSetFactory.Create(new TestLocalizationService()));

    private static SpineAssetGroupDto CreateGroup(params AssetChangeRecordDto[] changeRecords)
    {
        var bundleId = new BundleId("bundle-spine");
        return new SpineAssetGroupDto(
            GroupId: "hero",
            BundleId: bundleId,
            DisplayName: "hero",
            GroupKey: "characters/hero",
            SkeletonAssets: [],
            AtlasAssets:
            [
                new SpineAssetGroupPartDto(
                    SpineAssetGroupPartRole.Atlas,
                    bundleId,
                    new AssetId("hero-atlas"),
                    "spine.ab",
                    "hero.atlas",
                    "Assets/hero.atlas",
                    AssetType.TextAsset,
                    "TextAsset",
                    ChangeRecords: changeRecords)
            ],
            TexturePageAssets: [],
            Diagnostics: []);
    }

    private static AssetChangeRecordDto CreateTextEditRecord(
        int revision,
        bool isEffective = true) =>
        new(
            AssetChangeRecordKind.TextEdit,
            CreatedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(revision),
            AppliedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(revision),
            OperationId: $"edit-atlas-{revision}",
            Sequence: revision,
            DraftId: "draft-atlas",
            DraftMode: "text-content",
            IsEffectiveEdit: isEffective,
            TargetBundleId: "bundle-spine",
            TargetAssetId: "hero-atlas",
            TargetAssetName: "hero.atlas",
            TargetAssetType: AssetType.TextAsset.ToString(),
            DraftRevision: revision);
}
