using System.Runtime.CompilerServices;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class BundleExplorerSpineGroupDocumentCoordinatorTests
{
    [Fact]
    public async Task HandlePrimaryPaneSelectionChangedAsync_ShouldOpenSpineGroupDocumentAndReuseExistingTab()
    {
        var context = CreateContext();
        var coordinator = CreateCoordinator(context);
        var firstEntry = CreateSpineGroupEntry("hero", "Hero");
        context.PrimaryPane.ViewMode = AssetPaneViewMode.SpineGroups;
        context.PrimaryPane.ReplaceSpineGroups("ui.ab", [firstEntry], "empty");
        context.PrimaryPane.UpdateSpineGroupSelection(firstEntry);

        await coordinator.HandlePrimaryPaneSelectionChangedAsync(
            isSynchronizingPrimaryPaneState: false,
            captureActiveTabState: static () => { });

        var groupTab = Assert.Single(context.BundleTab.SpineGroupTabs);
        Assert.Same(groupTab, context.BundleTab.ActiveSpineGroupTab);
        Assert.True(groupTab.Inspector.HasSelection);
        Assert.Equal("Hero", groupTab.DisplayName);
        Assert.Equal(ResourceKeys.InspectorSpineGroupAssetType, groupTab.Inspector.AssetTypeText);

        var refreshedEntry = CreateSpineGroupEntry("hero", "Hero refreshed");
        context.PrimaryPane.ReplaceSpineGroups("ui.ab", [refreshedEntry], "empty");
        context.PrimaryPane.UpdateSpineGroupSelection(refreshedEntry);

        await coordinator.HandlePrimaryPaneSelectionChangedAsync(
            isSynchronizingPrimaryPaneState: false,
            captureActiveTabState: static () => { });

        Assert.Single(context.BundleTab.SpineGroupTabs);
        Assert.Same(groupTab, context.BundleTab.ActiveSpineGroupTab);
        Assert.Equal("Hero refreshed", groupTab.DisplayName);
    }

    [Fact]
    public async Task ActivateSpineGroupDocumentTabAsync_ShouldSwitchInspectorWithoutRemovingAssetTabs()
    {
        var context = CreateContext();
        var coordinator = CreateCoordinator(context);
        var assetTab = context.HostCoordinator.CreateAssetDocumentTab(
            context.BundleTab.BundleId,
            new AssetId("asset-texture"),
            "Texture");
        var groupTab = context.HostCoordinator.CreateSpineGroupDocumentTab(
            CreateSpineGroup("hero", "Hero"));
        context.BundleTab.AddAssetTab(assetTab, activate: true);
        context.BundleTab.AddSpineGroupTab(groupTab, activate: false);

        await coordinator.ActivateSpineGroupDocumentTabAsync(groupTab);

        Assert.Null(context.BundleTab.ActiveAssetTab);
        Assert.Same(groupTab, context.BundleTab.ActiveSpineGroupTab);
        Assert.Single(context.BundleTab.AssetTabs);
        Assert.Same(groupTab.Inspector, context.HostCoordinator.CurrentInspector);
    }

    [Fact]
    public async Task HandlePrimaryPaneSelectionChangedAsync_ShouldApplyLoadedAtlasDetails()
    {
        var context = CreateContext();
        var entry = CreateSpineGroupEntry("hero", "Hero");
        var loader = new TestSpineAssetGroupDetailsLoader
        {
            LoadHandler = tab => Task.FromResult<SpineAssetGroupDetailsLoadResult?>(new SpineAssetGroupDetailsLoadResult(
                new SpineAssetGroupDetailsDto(
                    tab.Item,
                    [CreateJsonSkeletonSummary(tab.Item.SkeletonAssets[0])],
                    [
                        new SpineAtlasPageDto(
                            "hero.png",
                            Width: 512,
                            Height: 256,
                            Regions:
                            [
                                new SpineAtlasRegionDto("head", "hero.png", 1, 2, 32, 48, IsRotated: false),
                                new SpineAtlasRegionDto("body", "hero.png", 4, 8, 64, 96, IsRotated: true)
                            ])
                    ],
                    tab.Item.Diagnostics),
                TexturePreview: null))
        };
        var coordinator = CreateCoordinator(context, loader);
        context.PrimaryPane.ViewMode = AssetPaneViewMode.SpineGroups;
        context.PrimaryPane.ReplaceSpineGroups("ui.ab", [entry], "empty");
        context.PrimaryPane.UpdateSpineGroupSelection(entry);

        await coordinator.HandlePrimaryPaneSelectionChangedAsync(
            isSynchronizingPrimaryPaneState: false,
            captureActiveTabState: static () => { });

        var groupTab = Assert.Single(context.BundleTab.SpineGroupTabs);
        Assert.Equal(1, loader.LoadCallCount);
        Assert.NotNull(groupTab.Details);
        Assert.Single(groupTab.Details.AtlasPages);
        Assert.Equal(2, groupTab.Details.AtlasPages[0].Regions.Count);
        Assert.Equal(
            ResourceKeys.InspectorSpineGroupSummaryFormat + ":1|1|1|1|2|0",
            groupTab.Inspector.Preview.PreviewText);
        Assert.Contains(
            groupTab.Inspector.Preview.PreviewInfoRows,
            row => row.Label == ResourceKeys.InspectorSpineGroupSkeletonStructureLabel &&
                   row.Value == ResourceKeys.InspectorSpineGroupSkeletonStructureFormat +
                   ":" + ResourceKeys.InspectorSpineGroupSkeletonFormatJson + "|2|3|1|4");
    }

    [Fact]
    public async Task HandlePrimaryPaneSelectionChangedAsync_ShouldApplyTexturePreviewWithRegionOverlay()
    {
        var context = CreateContext();
        var entry = CreateSpineGroupEntry("hero", "Hero");
        var texturePageAsset = entry.Item.TexturePageAssets[0];
        var atlasPage = new SpineAtlasPageDto(
            "hero.png",
            Width: 128,
            Height: 64,
            Regions:
            [
                new SpineAtlasRegionDto("head", "hero.png", 8, 4, 32, 24, IsRotated: false),
                new SpineAtlasRegionDto("body", "hero.png", 48, 16, 40, 32, IsRotated: false)
            ],
            TexturePageAsset: texturePageAsset);
        var preview = CreateImagePreview(texturePageAsset);
        var loader = new TestSpineAssetGroupDetailsLoader
        {
            LoadHandler = tab => Task.FromResult<SpineAssetGroupDetailsLoadResult?>(new SpineAssetGroupDetailsLoadResult(
                new SpineAssetGroupDetailsDto(tab.Item, [], [atlasPage], tab.Item.Diagnostics),
                new SpineAssetGroupTexturePreviewState(
                    atlasPage,
                    preview,
                    CreateImageSourceStub(),
                    PixelWidth: 128,
                    PixelHeight: 64)))
        };
        var coordinator = CreateCoordinator(context, loader);
        context.PrimaryPane.ViewMode = AssetPaneViewMode.SpineGroups;
        context.PrimaryPane.ReplaceSpineGroups("ui.ab", [entry], "empty");
        context.PrimaryPane.UpdateSpineGroupSelection(entry);

        await coordinator.HandlePrimaryPaneSelectionChangedAsync(
            isSynchronizingPrimaryPaneState: false,
            captureActiveTabState: static () => { });
        var groupTab = Assert.Single(context.BundleTab.SpineGroupTabs);
        groupTab.Inspector.UpdatePreviewImageViewport(256d, 128d);

        Assert.Equal(Visibility.Visible, groupTab.Inspector.Preview.PreviewImageVisibility);
        Assert.Equal(2, groupTab.Inspector.Preview.Image.RegionOverlays.Count);
        Assert.Equal(
            ResourceKeys.InspectorSpineGroupRegionOverlayBadgeFormat + ":hero.png|2",
            groupTab.Inspector.Preview.Image.RegionOverlayBadgeText);
        Assert.Equal(Visibility.Visible, groupTab.Inspector.Preview.Image.RegionOverlayVisibility);
    }

    [Fact]
    public async Task SelectActiveAtlasPageAsync_ShouldSwitchTexturePreviewOverlay()
    {
        var context = CreateContext();
        var entry = CreateSpineGroupEntry("hero", "Hero");
        var texturePageAsset = entry.Item.TexturePageAssets[0];
        var firstPage = new SpineAtlasPageDto(
            "hero-body.png",
            Width: 128,
            Height: 64,
            Regions:
            [
                new SpineAtlasRegionDto("body", "hero-body.png", 8, 4, 32, 24, IsRotated: false)
            ],
            TexturePageAsset: texturePageAsset);
        var secondPage = new SpineAtlasPageDto(
            "hero-face.png",
            Width: 256,
            Height: 128,
            Regions:
            [
                new SpineAtlasRegionDto("eye", "hero-face.png", 16, 8, 40, 32, IsRotated: false),
                new SpineAtlasRegionDto("mouth", "hero-face.png", 80, 32, 48, 24, IsRotated: false)
            ],
            TexturePageAsset: texturePageAsset);
        var loader = new TestSpineAssetGroupDetailsLoader
        {
            LoadHandler = tab => Task.FromResult<SpineAssetGroupDetailsLoadResult?>(new SpineAssetGroupDetailsLoadResult(
                new SpineAssetGroupDetailsDto(tab.Item, [], [firstPage, secondPage], tab.Item.Diagnostics),
                CreateTexturePreviewState(firstPage, texturePageAsset))),
            LoadTexturePreviewHandler = (_, option) => Task.FromResult<SpineAssetGroupTexturePreviewState?>(
                CreateTexturePreviewState(option.AtlasPage, texturePageAsset))
        };
        var coordinator = CreateCoordinator(context, loader);
        context.PrimaryPane.ViewMode = AssetPaneViewMode.SpineGroups;
        context.PrimaryPane.ReplaceSpineGroups("ui.ab", [entry], "empty");
        context.PrimaryPane.UpdateSpineGroupSelection(entry);

        await coordinator.HandlePrimaryPaneSelectionChangedAsync(
            isSynchronizingPrimaryPaneState: false,
            captureActiveTabState: static () => { });
        var groupTab = Assert.Single(context.BundleTab.SpineGroupTabs);
        var secondOption = Assert.Single(
            groupTab.AtlasPageOptions,
            option => option.DisplayName == "hero-face.png");

        await coordinator.SelectActiveAtlasPageAsync(secondOption);
        groupTab.Inspector.UpdatePreviewImageViewport(512d, 256d);

        Assert.Equal(1, loader.LoadTexturePreviewCallCount);
        Assert.Same(secondOption, groupTab.SelectedAtlasPageOption);
        Assert.Same(secondPage, groupTab.TexturePreview?.AtlasPage);
        Assert.Equal(2, groupTab.Inspector.Preview.Image.RegionOverlays.Count);
        Assert.Equal(
            ResourceKeys.InspectorSpineGroupRegionOverlayBadgeFormat + ":hero-face.png|2",
            groupTab.Inspector.Preview.Image.RegionOverlayBadgeText);
        Assert.Equal("hero-face.png", groupTab.Inspector.SpineAtlasPageSelector.SelectedOption?.DisplayName);
    }

    private static TestContext CreateContext()
    {
        var localizationService = new TestLocalizationService();
        var documentWorkspace = new BundleExplorerDocumentWorkspaceViewModel();
        var bundleTab = new BundleDocumentTabViewModel(new BundleId("bundle-ui"), "ui.ab", "ui.ab", isPreview: false);
        documentWorkspace.BundleTabs.Add(bundleTab);
        documentWorkspace.SetActiveBundleTab(bundleTab);
        var hostCoordinator = new BundleExplorerAssetDocumentHostCoordinator(
            documentWorkspace,
            localizationService,
            static () => { },
            static () => { });

        return new TestContext(
            localizationService,
            documentWorkspace,
            bundleTab,
            hostCoordinator,
            new AssetPaneViewModel());
    }

    private static BundleExplorerSpineGroupDocumentCoordinator CreateCoordinator(
        TestContext context,
        ISpineAssetGroupDetailsLoader? loader = null)
    {
        var bundlePane = new BundleExplorerBundlePaneViewModel(context.LocalizationService);
        return new BundleExplorerSpineGroupDocumentCoordinator(
            context.DocumentWorkspace,
            context.PrimaryPane,
            context.HostCoordinator,
            new SpineAssetGroupInspectorPresenter(bundlePane, context.LocalizationService),
            loader ?? new TestSpineAssetGroupDetailsLoader(),
            static () => { },
            static () => { });
    }

    private static SpineAssetGroupEntryViewModel CreateSpineGroupEntry(string groupId, string displayName)
    {
        return new SpineAssetGroupEntryViewModel(
            CreateSpineGroup(groupId, displayName),
            "Skeleton 1 / Atlas 1",
            "1 page",
            "Ready");
    }

    private static SpineAssetGroupDto CreateSpineGroup(string groupId, string displayName)
    {
        var bundleId = new BundleId("bundle-ui");
        return new SpineAssetGroupDto(
            groupId,
            bundleId,
            displayName,
            $"characters/{groupId}",
            SkeletonAssets:
            [
                new SpineAssetGroupPartDto(
                    SpineAssetGroupPartRole.Skeleton,
                    bundleId,
                    new AssetId($"{groupId}-json"),
                    "ui.ab",
                    $"{groupId}.json",
                    $"Assets/{groupId}.json",
                    AssetType.TextAsset,
                    "TextAsset")
            ],
            AtlasAssets:
            [
                new SpineAssetGroupPartDto(
                    SpineAssetGroupPartRole.Atlas,
                    bundleId,
                    new AssetId($"{groupId}-atlas"),
                    "ui.ab",
                    $"{groupId}.atlas",
                    $"Assets/{groupId}.atlas",
                    AssetType.TextAsset,
                    "TextAsset")
            ],
            TexturePageAssets:
            [
                new SpineAssetGroupPartDto(
                    SpineAssetGroupPartRole.TexturePage,
                    bundleId,
                    new AssetId($"{groupId}-texture"),
                    "ui.ab",
                    $"{groupId}.png",
                    $"Assets/{groupId}.png",
                    AssetType.Texture2D,
                    "Texture2D")
            ],
            Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>());
    }

    private static AssetPreviewDto CreateImagePreview(SpineAssetGroupPartDto texturePageAsset)
    {
        return new AssetPreviewDto(
            WorkspaceId: new WorkspaceId("workspace"),
            texturePageAsset.BundleId,
            texturePageAsset.AssetId,
            AssetPreviewKind.Image,
            ContentType: "image/png",
            BinaryContent: null,
            TextContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: null,
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UnixEpoch,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None));
    }

    private static SpineSkeletonSummaryDto CreateJsonSkeletonSummary(
        SpineAssetGroupPartDto skeletonAsset) =>
        new(
            skeletonAsset,
            SpineSkeletonAssetFormat.Json,
            SpineVersion: "4.1",
            BoneCount: 2,
            SlotCount: 3,
            SkinCount: 1,
            AnimationCount: 4,
            EventCount: 1,
            IkConstraintCount: 0,
            TransformConstraintCount: 0,
            PathConstraintCount: 0);

    private static SpineAssetGroupTexturePreviewState CreateTexturePreviewState(
        SpineAtlasPageDto atlasPage,
        SpineAssetGroupPartDto texturePageAsset)
    {
        return new SpineAssetGroupTexturePreviewState(
            atlasPage,
            CreateImagePreview(texturePageAsset),
            CreateImageSourceStub(),
            PixelWidth: atlasPage.Width ?? 1,
            PixelHeight: atlasPage.Height ?? 1);
    }

    private static ImageSource CreateImageSourceStub()
    {
        return (BitmapImage)RuntimeHelpers.GetUninitializedObject(typeof(BitmapImage));
    }

    private sealed record TestContext(
        TestLocalizationService LocalizationService,
        BundleExplorerDocumentWorkspaceViewModel DocumentWorkspace,
        BundleDocumentTabViewModel BundleTab,
        BundleExplorerAssetDocumentHostCoordinator HostCoordinator,
        AssetPaneViewModel PrimaryPane);

    private sealed class TestSpineAssetGroupDetailsLoader : ISpineAssetGroupDetailsLoader
    {
        public int LoadCallCount { get; private set; }

        public int LoadTexturePreviewCallCount { get; private set; }

        public Func<SpineAssetGroupDocumentTabViewModel, Task<SpineAssetGroupDetailsLoadResult?>> LoadHandler { get; init; } =
            _ => Task.FromResult<SpineAssetGroupDetailsLoadResult?>(null);

        public Func<SpineAssetGroupDocumentTabViewModel, SpineAtlasPageOptionViewModel, Task<SpineAssetGroupTexturePreviewState?>> LoadTexturePreviewHandler { get; init; } =
            (_, _) => Task.FromResult<SpineAssetGroupTexturePreviewState?>(null);

        public Task<SpineAssetGroupDetailsLoadResult?> LoadAsync(
            SpineAssetGroupDocumentTabViewModel groupTab,
            CancellationToken cancellationToken = default)
        {
            LoadCallCount++;
            return LoadHandler(groupTab);
        }

        public Task<SpineAssetGroupTexturePreviewState?> LoadTexturePreviewAsync(
            SpineAssetGroupDocumentTabViewModel groupTab,
            SpineAtlasPageOptionViewModel atlasPageOption,
            CancellationToken cancellationToken = default)
        {
            LoadTexturePreviewCallCount++;
            return LoadTexturePreviewHandler(groupTab, atlasPageOption);
        }
    }
}
