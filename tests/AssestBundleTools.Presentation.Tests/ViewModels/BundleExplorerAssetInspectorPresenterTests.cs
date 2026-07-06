using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.State;
using static AssestBundleTools.Presentation.Tests.ViewModels.BundleExplorerPreviewTestSupport;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class BundleExplorerAssetInspectorPresenterTests
{
    [Fact]
    public void CreateAssetTypeOptions_ShouldProjectDynamicFacets_WithPreferredOrderingAndCounts()
    {
        var localizationService = new TestLocalizationService();
        var bundlePane = new BundleExplorerBundlePaneViewModel(localizationService);
        var presenter = new BundleExplorerAssetInspectorPresenter(bundlePane, localizationService);

        var options = presenter.CreateAssetTypeOptions(
        [
            new AssetTypeFacetDto(AssetType.Material, "Material", 2),
            new AssetTypeFacetDto(AssetType.Texture2D, "Texture2D", 3),
            new AssetTypeFacetDto(AssetType.Mesh, "Mesh", 0),
            new AssetTypeFacetDto(AssetType.Unknown, "Shader", 1)
        ]);

        Assert.Equal(4, options.Count);
        Assert.Equal(ResourceKeys.AssetTypeFilterAll, options[0].DisplayText);
        Assert.Null(options[0].FilterKey);
        Assert.Equal(AssetType.Texture2D, options[1].AssetType);
        Assert.Equal(ResourceKeys.AssetTypeDisplayTexture2D + " (3)", options[1].DisplayText);
        Assert.Equal("Texture2D", options[1].FilterKey);
        Assert.Equal(AssetType.Material, options[2].AssetType);
        Assert.Equal(ResourceKeys.AssetTypeDisplayMaterial + " (2)", options[2].DisplayText);
        Assert.DoesNotContain(options, option => option.AssetType == AssetType.Mesh);

        var shaderOption = options[3];
        Assert.Equal("Shader", shaderOption.DisplayName);
        Assert.Equal("Shader (1)", shaderOption.DisplayText);
        Assert.Equal(AssetType.Unknown, shaderOption.AssetType);
        Assert.Equal("Shader", shaderOption.AssetTypeDisplayNameFilter);
        Assert.Equal("Unknown:Shader", shaderOption.FilterKey);
    }

    [Fact]
    public void PresentAssetDocumentTab_ShouldProjectDependencyNavigationTarget()
    {
        var localizationService = new TestLocalizationService();
        var targetBundleId = new BundleId("bundle-textures");
        var targetAssetId = new AssetId("asset-texture");
        var bundlePane = new BundleExplorerBundlePaneViewModel(localizationService);
        bundlePane.RebuildBundleGroups(CreateBundleTree(targetBundleId, "textures.ab"));
        var presenter = new BundleExplorerAssetInspectorPresenter(bundlePane, localizationService);
        var assetTab = new AssetDocumentTabViewModel(
            new BundleId("bundle-materials"),
            new AssetId("asset-material"),
            "paopao_01",
            InspectorInformationTextSetFactory.Create(localizationService));
        var details = CreateAssetDetails(
            WorkspaceId.New(),
            assetTab.BundleId,
            assetTab.AssetId) with
        {
            Name = "paopao_01",
            AssetType = AssetType.Material,
            AssetTypeDisplayName = "Material",
            Dependencies =
            [
                new AssetDependencyDto(
                    targetAssetId,
                    targetBundleId,
                    "HeroTexture",
                    AssetType.Texture2D,
                    PathId: 200,
                    AssetTypeDisplayName: "Texture2D")
            ]
        };

        presenter.PresentAssetDocumentTab(assetTab, details, canEdit: true, isBusy: false);

        var dependency = Assert.Single(assetTab.Inspector.AssetDependencies);
        Assert.Equal("textures.ab", dependency.BundleName);
        Assert.Equal(ResourceKeys.InspectorResolvedAssetOpenButton, dependency.OpenButtonText);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, dependency.OpenButtonVisibility);
        Assert.NotNull(dependency.Target);
        Assert.Equal(targetAssetId, dependency.Target.AssetId);
        Assert.Equal(targetBundleId, dependency.Target.BundleId);
        Assert.Equal(200, dependency.Target.PathId);
        Assert.Equal("HeroTexture", dependency.Target.DisplayName);
        Assert.Equal("Texture2D", dependency.Target.AssetTypeText);
    }

    [Fact]
    public void PresentAssetDocumentTab_ShouldLabelOrdinaryModifyActionAsFileReplacement()
    {
        var localizationService = new TestLocalizationService();
        var bundlePane = new BundleExplorerBundlePaneViewModel(localizationService);
        var presenter = new BundleExplorerAssetInspectorPresenter(bundlePane, localizationService);
        var assetTab = new AssetDocumentTabViewModel(
            new BundleId("bundle-text"),
            new AssetId("asset-text"),
            "config.json",
            InspectorInformationTextSetFactory.Create(localizationService));
        var details = CreateAssetDetails(
            WorkspaceId.New(),
            assetTab.BundleId,
            assetTab.AssetId) with
        {
            Name = "config.json",
            AssetType = AssetType.TextAsset,
            AssetTypeDisplayName = "TextAsset"
        };

        presenter.PresentAssetDocumentTab(assetTab, details, canEdit: true, isBusy: false);

        Assert.Equal(ResourceKeys.InspectorReplaceFileButton, assetTab.Inspector.ModifyButtonText);
        Assert.Equal("\uE8AC", assetTab.Inspector.ModifyButtonIconGlyph);
    }

    [Fact]
    public void PresentAssetDocumentTab_ShouldKeepSpriteModifyActionAsEditEntry()
    {
        var localizationService = new TestLocalizationService();
        var bundlePane = new BundleExplorerBundlePaneViewModel(localizationService);
        var presenter = new BundleExplorerAssetInspectorPresenter(bundlePane, localizationService);
        var assetTab = new AssetDocumentTabViewModel(
            new BundleId("bundle-sprite"),
            new AssetId("asset-sprite"),
            "hero",
            InspectorInformationTextSetFactory.Create(localizationService));
        var details = CreateAssetDetails(
            WorkspaceId.New(),
            assetTab.BundleId,
            assetTab.AssetId) with
        {
            Name = "hero",
            AssetType = AssetType.Sprite,
            AssetTypeDisplayName = "Sprite"
        };

        presenter.PresentAssetDocumentTab(assetTab, details, canEdit: true, isBusy: false);

        Assert.Equal(ResourceKeys.InspectorModifyButton, assetTab.Inspector.ModifyButtonText);
        Assert.Equal("\uE70F", assetTab.Inspector.ModifyButtonIconGlyph);
    }

    [Fact]
    public void SpineGroupPresenter_ShouldEnableModifyWithoutEnablingReplacementSourceSelection()
    {
        var localizationService = new TestLocalizationService();
        var bundlePane = new BundleExplorerBundlePaneViewModel(localizationService);
        var presenter = new SpineAssetGroupInspectorPresenter(bundlePane, localizationService);
        var bundleId = new BundleId("bundle-spine");
        var groupTab = new SpineAssetGroupDocumentTabViewModel(
            new SpineAssetGroupDto(
                GroupId: "hero",
                BundleId: bundleId,
                DisplayName: "hero",
                GroupKey: "characters/hero",
                SkeletonAssets:
                [
                    new SpineAssetGroupPartDto(
                        SpineAssetGroupPartRole.Skeleton,
                        bundleId,
                        new AssetId("hero-json"),
                        "spine.ab",
                        "hero.json",
                        "Assets/hero.json",
                        AssetType.TextAsset,
                        "TextAsset")
                ],
                AtlasAssets: Array.Empty<SpineAssetGroupPartDto>(),
                TexturePageAssets: Array.Empty<SpineAssetGroupPartDto>(),
                Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>()),
            InspectorInformationTextSetFactory.Create(localizationService));

        presenter.PresentSpineGroupDocumentTab(groupTab);

        Assert.True(groupTab.Inspector.CanModify);
        Assert.False(groupTab.Inspector.CanSelectFileReplacementSource);
        Assert.Equal(ResourceKeys.InspectorModifyButton, groupTab.Inspector.ModifyButtonText);
        Assert.Equal("\uE70F", groupTab.Inspector.ModifyButtonIconGlyph);

        presenter.SyncInspectorActionStates(groupTab.Inspector, groupTab, isBusy: true);

        Assert.False(groupTab.Inspector.CanModify);
        Assert.False(groupTab.Inspector.CanSelectFileReplacementSource);
    }

    [Fact]
    public void SpineGroupPresenter_ShouldProjectPartReplacementRecordsToChanges()
    {
        var localizationService = new TestLocalizationService();
        var bundlePane = new BundleExplorerBundlePaneViewModel(localizationService);
        var presenter = new SpineAssetGroupInspectorPresenter(bundlePane, localizationService);
        var bundleId = new BundleId("bundle-spine");
        var appliedAtUtc = new DateTimeOffset(2026, 6, 4, 10, 0, 0, TimeSpan.Zero);
        var groupTab = new SpineAssetGroupDocumentTabViewModel(
            new SpineAssetGroupDto(
                GroupId: "hero",
                BundleId: bundleId,
                DisplayName: "hero",
                GroupKey: "characters/hero",
                SkeletonAssets: Array.Empty<SpineAssetGroupPartDto>(),
                AtlasAssets: Array.Empty<SpineAssetGroupPartDto>(),
                TexturePageAssets:
                [
                    new SpineAssetGroupPartDto(
                        SpineAssetGroupPartRole.TexturePage,
                        bundleId,
                        new AssetId("hero-texture"),
                        "spine.ab",
                        "hero.png",
                        "Assets/hero.png",
                        AssetType.Texture2D,
                        "Texture2D",
                        ChangeRecords:
                        [
                            new AssetChangeRecordDto(
                                AssetChangeRecordKind.FileReplacement,
                                CreatedAtUtc: appliedAtUtc,
                                AppliedAtUtc: appliedAtUtc,
                                SourceFileName: "hero-replace.png",
                                SourceFilePath: @"C:\Imports\hero-replace.png",
                                OperationId: "replace-texture",
                                IsEffectiveReplacement: true)
                        ])
                ],
                Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>()),
            InspectorInformationTextSetFactory.Create(localizationService));

        presenter.PresentSpineGroupDocumentTab(groupTab);

        var record = Assert.Single(groupTab.Inspector.ChangeRecords);
        Assert.Equal(ResourceKeys.InspectorChangeRecordAssociatedFileReplacementTitle, record.TitleText);
        Assert.Contains("hero.png", record.SecondaryText, StringComparison.Ordinal);
        Assert.Contains(@"C:\Imports\hero-replace.png", record.SecondaryText, StringComparison.Ordinal);
        Assert.Equal("bundle-spine", record.TargetBundleId);
        Assert.Equal("hero-texture", record.TargetAssetId);
        Assert.Equal("hero.png", record.TargetAssetName);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, groupTab.Inspector.ChangeRecordsListVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, groupTab.Inspector.ChangeRecordsEmptyStateVisibility);
        Assert.True(record.ShowsUndoReplacement);
        Assert.False(record.ShowsRedoReplacement);
        Assert.True(record.ShowsDeleteRecord);
        Assert.True(record.IsActionEnabled);
    }

    [Fact]
    public void SpineGroupPresenter_ShouldProjectPartTextEditRecordsWithTargetDetails()
    {
        var localizationService = new TestLocalizationService();
        var bundlePane = new BundleExplorerBundlePaneViewModel(localizationService);
        var presenter = new SpineAssetGroupInspectorPresenter(bundlePane, localizationService);
        var bundleId = new BundleId("bundle-spine");
        var appliedAtUtc = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
        var groupTab = new SpineAssetGroupDocumentTabViewModel(
            new SpineAssetGroupDto(
                GroupId: "hero",
                BundleId: bundleId,
                DisplayName: "hero",
                GroupKey: "characters/hero",
                SkeletonAssets: Array.Empty<SpineAssetGroupPartDto>(),
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
                        ChangeRecords:
                        [
                            new AssetChangeRecordDto(
                                AssetChangeRecordKind.TextEdit,
                                CreatedAtUtc: appliedAtUtc,
                                AppliedAtUtc: appliedAtUtc,
                                OperationId: "edit-atlas",
                                IsEffectiveEdit: true,
                                DraftId: "draft-atlas",
                                DraftMode: "text-content",
                                DraftRevision: 3)
                        ])
                ],
                TexturePageAssets: Array.Empty<SpineAssetGroupPartDto>(),
                Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>()),
            InspectorInformationTextSetFactory.Create(localizationService));

        presenter.PresentSpineGroupDocumentTab(groupTab);

        var record = Assert.Single(groupTab.Inspector.ChangeRecords);
        Assert.Equal(ResourceKeys.InspectorChangeRecordTextEditTitle, record.TitleText);
        Assert.Equal("bundle-spine", record.TargetBundleId);
        Assert.Equal("hero-atlas", record.TargetAssetId);
        Assert.Equal("hero.atlas", record.TargetAssetName);
        Assert.Contains("hero.atlas", record.SecondaryText, StringComparison.Ordinal);
        Assert.True(record.ShowsDeleteRecord);
    }

    [Fact]
    public void ChangeRecordPresenters_ShouldProjectPendingState_ForSameOperationAcrossAssetAndSpineGroup()
    {
        var localizationService = new TestLocalizationService();
        var bundlePane = new BundleExplorerBundlePaneViewModel(localizationService);
        var pendingTracker = new WorkspaceOperationPendingTracker();
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-spine");
        var operationId = "replace-texture";
        using var pendingScope = pendingTracker.Begin(workspaceId, operationId);
        var assetPresenter = new BundleExplorerAssetInspectorPresenter(
            bundlePane,
            localizationService,
            pendingTracker);
        var spinePresenter = new SpineAssetGroupInspectorPresenter(
            bundlePane,
            localizationService,
            pendingTracker,
            () => workspaceId);
        var changeRecord = new AssetChangeRecordDto(
            AssetChangeRecordKind.FileReplacement,
            CreatedAtUtc: DateTimeOffset.UnixEpoch,
            OperationId: operationId,
            IsEffectiveReplacement: true,
            TargetBundleId: bundleId.Value,
            TargetAssetId: "hero-texture",
            SourceFileName: "hero.png");
        var assetTab = new AssetDocumentTabViewModel(
            bundleId,
            new AssetId("hero-texture"),
            "hero.png",
            InspectorInformationTextSetFactory.Create(localizationService));
        var assetDetails = CreateAssetDetails(
            workspaceId,
            bundleId,
            assetTab.AssetId) with
        {
            IsModified = true,
            ChangeRecords = [changeRecord]
        };
        var groupTab = new SpineAssetGroupDocumentTabViewModel(
            new SpineAssetGroupDto(
                GroupId: "hero",
                BundleId: bundleId,
                DisplayName: "hero",
                GroupKey: "characters/hero",
                SkeletonAssets: Array.Empty<SpineAssetGroupPartDto>(),
                AtlasAssets: Array.Empty<SpineAssetGroupPartDto>(),
                TexturePageAssets:
                [
                    new SpineAssetGroupPartDto(
                        SpineAssetGroupPartRole.TexturePage,
                        bundleId,
                        new AssetId("hero-texture"),
                        "spine.ab",
                        "hero.png",
                        "Assets/hero.png",
                        AssetType.Texture2D,
                        "Texture2D",
                        ChangeRecords: [changeRecord])
                ],
                Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>()),
            InspectorInformationTextSetFactory.Create(localizationService));
        groupTab.ApplyDetails(
            new SpineAssetGroupDetailsDto(
                Group: groupTab.Item,
                AtlasPages: Array.Empty<SpineAtlasPageDto>(),
                SkeletonSummaries: Array.Empty<SpineSkeletonSummaryDto>(),
                Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>(),
                AnimationPreview: null),
            texturePreview: null,
            animationTextures: new Dictionary<string, SpineAnimationPreviewTextureState>());

        assetPresenter.PresentAssetDocumentTab(assetTab, assetDetails, canEdit: true, isBusy: false);
        spinePresenter.PresentSpineGroupDocumentTab(groupTab);

        var assetRecord = Assert.Single(assetTab.Inspector.ChangeRecords);
        var spineRecord = Assert.Single(groupTab.Inspector.ChangeRecords);
        Assert.True(assetRecord.IsPending);
        Assert.False(assetRecord.IsActionEnabled);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, assetRecord.PendingVisibility);
        Assert.True(spineRecord.IsPending);
        Assert.False(spineRecord.IsActionEnabled);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, spineRecord.PendingVisibility);
    }

    private static IReadOnlyList<BundleTreeNodeDto> CreateBundleTree(
        BundleId bundleId,
        string displayName)
    {
        return
        [
            new BundleTreeNodeDto(
                BundleId: new BundleId("group-root"),
                DisplayName: "Root",
                LogicalPath: "Root",
                SourcePath: string.Empty,
                IsVirtualFolder: true,
                AssetCount: 0,
                Children:
                [
                    new BundleTreeNodeDto(
                        BundleId: bundleId,
                        DisplayName: displayName,
                        LogicalPath: displayName,
                        SourcePath: $@"C:\Bundles\{displayName}",
                        IsVirtualFolder: false,
                        AssetCount: 1,
                        Children: Array.Empty<BundleTreeNodeDto>())
                ])
        ];
    }
}
