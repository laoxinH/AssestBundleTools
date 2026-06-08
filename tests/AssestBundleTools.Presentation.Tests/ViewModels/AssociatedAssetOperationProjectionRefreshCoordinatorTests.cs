using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class AssociatedAssetOperationProjectionRefreshCoordinatorTests
{
    [Fact]
    public async Task RefreshSpineGroupOperationProjectionAsync_ShouldUpdateChangeRecordActionsWithoutReloadingPreviewDetails()
    {
        var localizationService = new TestLocalizationService();
        var bundlePane = new BundleExplorerBundlePaneViewModel(localizationService);
        var assetPresenter = new BundleExplorerAssetInspectorPresenter(bundlePane, localizationService);
        var presenter = new SpineAssetGroupInspectorPresenter(bundlePane, localizationService);
        var group = CreateSpineGroup(
            new AssetChangeRecordDto(
                AssetChangeRecordKind.FileReplacement,
                CreatedAtUtc: DateTimeOffset.UnixEpoch,
                OperationId: "replace-texture",
                IsEffectiveReplacement: true,
                TargetBundleId: "bundle-ui",
                TargetAssetId: "hero-texture",
                SourceFileName: "old.png"));
        var groupTab = new SpineAssetGroupDocumentTabViewModel(
            group,
            InspectorInformationTextSetFactory.Create(localizationService));
        presenter.PresentSpineGroupDocumentTab(groupTab);
        groupTab.Inspector.SetPreviewLoading("existing-preview-loading");
        Assert.True(Assert.Single(groupTab.Inspector.ChangeRecords).ShowsUndoReplacement);
        Assert.True(groupTab.Inspector.Preview.IsPreviewLoading);
        Assert.Equal("existing-preview-loading", groupTab.Inspector.Preview.PreviewStatusText);

        var gateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetDetailsHandler = query => Task.FromResult(Result<AssetDetailsDto>.Success(CreateTextureDetails(
                query.BundleId,
                query.AssetId,
                new AssetChangeRecordDto(
                    AssetChangeRecordKind.FileReplacement,
                    CreatedAtUtc: DateTimeOffset.UnixEpoch,
                    OperationId: "replace-texture",
                    IsEffectiveReplacement: false,
                    TargetBundleId: query.BundleId.Value,
                    TargetAssetId: query.AssetId.Value,
                    SourceFileName: "old.png"))))
        };
        var coordinator = new AssociatedAssetOperationProjectionRefreshCoordinator(
            gateway,
            new TestBackgroundTaskRunner(),
            assetPresenter,
            presenter,
            () => new WorkspaceId("workspace"),
            () => false,
            _ => { });

        var updated = await coordinator.RefreshSpineGroupOperationProjectionAsync(
            groupTab,
            [new WorkspaceOperationAffectedAsset(new BundleId("bundle-ui"), new AssetId("hero-texture"))]);

        Assert.True(updated);
        var record = Assert.Single(groupTab.Inspector.ChangeRecords);
        Assert.False(record.ShowsUndoReplacement);
        Assert.True(record.ShowsRedoReplacement);
        Assert.NotNull(groupTab.Item.TexturePageAssets[0].ChangeRecords);
        Assert.False(groupTab.Item.TexturePageAssets[0].ChangeRecords![0].IsEffectiveReplacement);
        Assert.True(groupTab.Inspector.Preview.IsPreviewLoading);
        Assert.Equal("existing-preview-loading", groupTab.Inspector.Preview.PreviewStatusText);
    }

    [Fact]
    public async Task RefreshAssetDocumentOperationProjectionAsync_ShouldUpdateAssociatedTextAssetRecordActions()
    {
        var localizationService = new TestLocalizationService();
        var bundlePane = new BundleExplorerBundlePaneViewModel(localizationService);
        var assetPresenter = new BundleExplorerAssetInspectorPresenter(bundlePane, localizationService);
        var spinePresenter = new SpineAssetGroupInspectorPresenter(bundlePane, localizationService);
        var hostDetails = CreateAssetDetails(
            new BundleId("bundle-ui"),
            new AssetId("hero-host"),
            "hero.prefab",
            AssetType.Unknown,
            "MonoBehaviour",
            new AssetChangeRecordDto(
                AssetChangeRecordKind.AssociatedFileReplacement,
                CreatedAtUtc: DateTimeOffset.UnixEpoch,
                OperationId: "replace-skeleton",
                IsEffectiveReplacement: true,
                TargetBundleId: "bundle-ui",
                TargetAssetId: "hero-skeleton",
                SourceFileName: "hero.skel",
                IsRelatedOperation: true));
        var assetTab = new AssetDocumentTabViewModel(
            hostDetails.BundleId,
            hostDetails.AssetId,
            hostDetails.Name,
            InspectorInformationTextSetFactory.Create(localizationService));
        assetPresenter.PresentAssetDocumentTab(assetTab, hostDetails, canEdit: true, isBusy: false);
        Assert.True(Assert.Single(assetTab.Inspector.ChangeRecords).ShowsUndoReplacement);

        var gateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetDetailsHandler = _ => Task.FromResult(Result<AssetDetailsDto>.Success(CreateAssetDetails(
                hostDetails.BundleId,
                hostDetails.AssetId,
                hostDetails.Name,
                hostDetails.AssetType,
                hostDetails.AssetTypeDisplayName ?? "MonoBehaviour",
                new AssetChangeRecordDto(
                    AssetChangeRecordKind.AssociatedFileReplacement,
                    CreatedAtUtc: DateTimeOffset.UnixEpoch,
                    OperationId: "replace-skeleton",
                    IsEffectiveReplacement: false,
                    TargetBundleId: "bundle-ui",
                    TargetAssetId: "hero-skeleton",
                    SourceFileName: "hero.skel",
                    IsRelatedOperation: true))))
        };
        var coordinator = new AssociatedAssetOperationProjectionRefreshCoordinator(
            gateway,
            new TestBackgroundTaskRunner(),
            assetPresenter,
            spinePresenter,
            () => new WorkspaceId("workspace"),
            () => false,
            _ => { });

        var updated = await coordinator.RefreshAssetDocumentOperationProjectionAsync(assetTab);

        Assert.True(updated);
        var record = Assert.Single(assetTab.Inspector.ChangeRecords);
        Assert.False(record.ShowsUndoReplacement);
        Assert.True(record.ShowsRedoReplacement);
        Assert.NotNull(assetTab.Details?.ChangeRecords);
        Assert.False(assetTab.Details!.ChangeRecords![0].IsEffectiveReplacement);
        Assert.Equal("hero-skeleton", assetTab.Details.ChangeRecords[0].TargetAssetId);
    }

    private static SpineAssetGroupDto CreateSpineGroup(AssetChangeRecordDto textureChangeRecord)
    {
        var bundleId = new BundleId("bundle-ui");
        return new SpineAssetGroupDto(
            "hero",
            bundleId,
            "Hero",
            "characters/hero",
            SkeletonAssets:
            [
                new SpineAssetGroupPartDto(
                    SpineAssetGroupPartRole.Skeleton,
                    bundleId,
                    new AssetId("hero-json"),
                    "ui.ab",
                    "hero.json",
                    "Assets/hero.json",
                    AssetType.TextAsset,
                    "TextAsset")
            ],
            AtlasAssets:
            [
                new SpineAssetGroupPartDto(
                    SpineAssetGroupPartRole.Atlas,
                    bundleId,
                    new AssetId("hero-atlas"),
                    "ui.ab",
                    "hero.atlas",
                    "Assets/hero.atlas",
                    AssetType.TextAsset,
                    "TextAsset")
            ],
            TexturePageAssets:
            [
                new SpineAssetGroupPartDto(
                    SpineAssetGroupPartRole.TexturePage,
                    bundleId,
                    new AssetId("hero-texture"),
                    "ui.ab",
                    "hero.png",
                    "Assets/hero.png",
                    AssetType.Texture2D,
                    "Texture2D",
                    ChangeRecords: [textureChangeRecord])
            ],
            Diagnostics: []);
    }

    private static AssetDetailsDto CreateTextureDetails(
        BundleId bundleId,
        AssetId assetId,
        AssetChangeRecordDto changeRecord) =>
        CreateAssetDetails(
            bundleId,
            assetId,
            "hero.png",
            AssetType.Texture2D,
            "Texture2D",
            changeRecord);

    private static AssetDetailsDto CreateAssetDetails(
        BundleId bundleId,
        AssetId assetId,
        string name,
        AssetType assetType,
        string assetTypeDisplayName,
        AssetChangeRecordDto changeRecord) =>
        new(
            WorkspaceId: new WorkspaceId("workspace"),
            BundleId: bundleId,
            AssetId: assetId,
            Name: name,
            AssetPath: $"Assets/{name}",
            ContainerPath: "CAB",
            AssetType: assetType,
            AssetTypeDisplayName: assetTypeDisplayName,
            SerializedFileName: "ui.ab",
            PathId: 1,
            SizeInBytes: 1,
            PreferredDisplaySizeInBytes: null,
            IsModified: true,
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
            Dependencies: [],
            CanRedoReplacement: !changeRecord.IsEffectiveReplacement,
            ChangeRecords: [changeRecord]);
}
