using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
using System.Globalization;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed partial class BundleExplorerSpineGroupDocumentCoordinatorTests
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
    public async Task HandlePrimaryPaneSelectionChangedAsync_ShouldReuseLoadedDetails_WhenExistingTabSnapshotUnchanged()
    {
        var context = CreateContext();
        var loader = new TestSpineAssetGroupDetailsLoader
        {
            LoadHandler = tab => Task.FromResult<SpineAssetGroupDetailsLoadResult?>(new SpineAssetGroupDetailsLoadResult(
                new SpineAssetGroupDetailsDto(tab.Item, [], [], tab.Item.Diagnostics),
                TexturePreview: null))
        };
        var coordinator = CreateCoordinator(context, loader);
        var entry = CreateSpineGroupEntry("hero", "Hero");
        context.PrimaryPane.ViewMode = AssetPaneViewMode.SpineGroups;
        context.PrimaryPane.ReplaceSpineGroups("ui.ab", [entry], "empty");
        context.PrimaryPane.UpdateSpineGroupSelection(entry);
        await coordinator.HandlePrimaryPaneSelectionChangedAsync(
            isSynchronizingPrimaryPaneState: false,
            captureActiveTabState: static () => { });

        var groupTab = Assert.Single(context.BundleTab.SpineGroupTabs);
        Assert.NotNull(groupTab.Details);

        await coordinator.HandlePrimaryPaneSelectionChangedAsync(
            isSynchronizingPrimaryPaneState: false,
            captureActiveTabState: static () => { });

        Assert.Equal(1, loader.LoadCallCount);
        Assert.NotNull(groupTab.Details);
        Assert.False(groupTab.Inspector.Preview.IsPreviewLoading);
    }

    [Fact]
    public async Task ActivateSpineGroupDocumentTabAsync_ShouldRefreshExtractionSummaryFromPane()
    {
        var context = CreateContext();
        var loader = new TestSpineAssetGroupDetailsLoader
        {
            LoadHandler = tab => Task.FromResult<SpineAssetGroupDetailsLoadResult?>(new SpineAssetGroupDetailsLoadResult(
                new SpineAssetGroupDetailsDto(tab.Item, [], [], tab.Item.Diagnostics),
                TexturePreview: null))
        };
        var coordinator = CreateCoordinator(context, loader);
        var firstEntry = CreateSpineGroupEntry("hero", "Hero");
        context.PrimaryPane.ViewMode = AssetPaneViewMode.SpineGroups;
        context.PrimaryPane.ReplaceSpineGroups("ui.ab", [firstEntry], "empty");
        context.PrimaryPane.UpdateSpineGroupSelection(firstEntry);
        await coordinator.HandlePrimaryPaneSelectionChangedAsync(
            isSynchronizingPrimaryPaneState: false,
            captureActiveTabState: static () => { });

        var groupTab = Assert.Single(context.BundleTab.SpineGroupTabs);
        Assert.Equal(ResourceKeys.InspectorChangeSummaryNone, groupTab.Inspector.ChangeSummaryText);
        Assert.Equal(1, loader.LoadCallCount);

        var extractedAtUtc = new DateTimeOffset(2026, 5, 12, 8, 30, 0, TimeSpan.Zero);
        var refreshedEntry = CreateSpineGroupEntry("hero", "Hero", extractedAtUtc);
        context.PrimaryPane.ReplaceSpineGroups("ui.ab", [refreshedEntry], "empty");

        await coordinator.ActivateSpineGroupDocumentTabAsync(groupTab);

        Assert.Equal(2, loader.LoadCallCount);
        Assert.Equal(extractedAtUtc, groupTab.Item.SkeletonAssets[0].LastExtractedAtUtc);
        Assert.Equal(
            ResourceKeys.InspectorChangeSummaryExtractOnlyFormat + ":" +
            extractedAtUtc.ToLocalTime().ToString("G", CultureInfo.CurrentCulture),
            groupTab.Inspector.ChangeSummaryText);
    }

    [Fact]
    public async Task RefreshSpineGroupDocumentTabAsync_ShouldRefreshInactiveOpenTab()
    {
        var context = CreateContext();
        var extractedAtUtc = new DateTimeOffset(2026, 5, 12, 9, 45, 0, TimeSpan.Zero);
        var loader = new TestSpineAssetGroupDetailsLoader
        {
            LoadHandler = tab =>
            {
                var refreshedGroup = CreateSpineGroup(tab.GroupId, tab.DisplayName, extractedAtUtc);
                return Task.FromResult<SpineAssetGroupDetailsLoadResult?>(new SpineAssetGroupDetailsLoadResult(
                    new SpineAssetGroupDetailsDto(refreshedGroup, [], [], refreshedGroup.Diagnostics),
                    TexturePreview: null));
            }
        };
        var coordinator = CreateCoordinator(context, loader);
        var activeTab = context.HostCoordinator.CreateSpineGroupDocumentTab(
            CreateSpineGroup("hero", "Hero"));
        var inactiveTab = context.HostCoordinator.CreateSpineGroupDocumentTab(
            CreateSpineGroup("enemy", "Enemy"));
        context.BundleTab.AddSpineGroupTab(activeTab, activate: true);
        context.BundleTab.AddSpineGroupTab(inactiveTab, activate: false);

        await coordinator.RefreshSpineGroupDocumentTabAsync(inactiveTab);

        Assert.Equal(1, loader.LoadCallCount);
        Assert.Same(activeTab, context.BundleTab.ActiveSpineGroupTab);
        Assert.Equal(extractedAtUtc, inactiveTab.Item.SkeletonAssets[0].LastExtractedAtUtc);
        Assert.Equal(
            ResourceKeys.InspectorChangeSummaryExtractOnlyFormat + ":" +
            extractedAtUtc.ToLocalTime().ToString("G", CultureInfo.CurrentCulture),
            inactiveTab.Inspector.ChangeSummaryText);
    }

    [Fact]
    public async Task RefreshSpineGroupDocumentTabAsync_ShouldShowLoadingWhileReloadingExistingPreview()
    {
        var context = CreateContext();
        var loadCompletion = new TaskCompletionSource<SpineAssetGroupDetailsLoadResult?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new TestSpineAssetGroupDetailsLoader
        {
            LoadHandler = tab => loadCompletion.Task
        };
        var coordinator = CreateCoordinator(context, loader);
        var groupTab = context.HostCoordinator.CreateSpineGroupDocumentTab(
            CreateSpineGroup("hero", "Hero"));
        groupTab.ApplyDetails(new SpineAssetGroupDetailsDto(groupTab.Item, [], [], groupTab.Item.Diagnostics));
        new SpineAssetGroupInspectorPresenter(
                new BundleExplorerBundlePaneViewModel(context.LocalizationService),
                context.LocalizationService)
            .PresentSpineGroupDocumentTab(groupTab);
        context.BundleTab.AddSpineGroupTab(groupTab, activate: true);

        var refreshTask = coordinator.RefreshSpineGroupDocumentTabAsync(groupTab);

        Assert.False(refreshTask.IsCompleted);
        Assert.True(groupTab.Inspector.Preview.IsPreviewLoading);
        Assert.Equal(ResourceKeys.InspectorPreviewLoadingText, groupTab.Inspector.Preview.PreviewStatusText);

        loadCompletion.SetResult(new SpineAssetGroupDetailsLoadResult(
            new SpineAssetGroupDetailsDto(groupTab.Item, [], [], groupTab.Item.Diagnostics),
            TexturePreview: null));
        await refreshTask;

        Assert.False(groupTab.Inspector.Preview.IsPreviewLoading);
    }

    [Fact]
    public async Task HandlePrimaryPaneSelectionChangedAsync_ShouldShowLoadingWhileSpineDetailsArePending()
    {
        var context = CreateContext();
        var entry = CreateSpineGroupEntry("hero", "Hero");
        var loadCompletion = new TaskCompletionSource<SpineAssetGroupDetailsLoadResult?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new TestSpineAssetGroupDetailsLoader
        {
            LoadHandler = _ => loadCompletion.Task
        };
        var coordinator = CreateCoordinator(context, loader);
        context.PrimaryPane.ViewMode = AssetPaneViewMode.SpineGroups;
        context.PrimaryPane.ReplaceSpineGroups("ui.ab", [entry], "empty");
        context.PrimaryPane.UpdateSpineGroupSelection(entry);

        var loadTask = coordinator.HandlePrimaryPaneSelectionChangedAsync(
            isSynchronizingPrimaryPaneState: false,
            captureActiveTabState: static () => { });

        var groupTab = Assert.Single(context.BundleTab.SpineGroupTabs);
        Assert.False(loadTask.IsCompleted);
        Assert.True(groupTab.Inspector.Preview.IsPreviewLoading);
        Assert.Equal(ResourceKeys.InspectorPreviewLoadingText, groupTab.Inspector.Preview.PreviewStatusText);
        Assert.Equal(Visibility.Visible, groupTab.Inspector.Preview.PreviewLoadingVisibility);
        Assert.Equal(Visibility.Collapsed, groupTab.Inspector.Preview.PreviewMessageVisibility);

        loadCompletion.SetResult(new SpineAssetGroupDetailsLoadResult(
            new SpineAssetGroupDetailsDto(groupTab.Item, [], [], groupTab.Item.Diagnostics),
            TexturePreview: null));
        await loadTask;

        Assert.False(groupTab.Inspector.Preview.IsPreviewLoading);
    }

    [Fact]
    public async Task ActivateSpineGroupDocumentTabAsync_ShouldReusePendingDetailsLoad_WhenNewTabSelectionEventFiresBeforeLoadCompletes()
    {
        var context = CreateContext();
        var entry = CreateSpineGroupEntry("hero", "Hero");
        var loadCompletion = new TaskCompletionSource<SpineAssetGroupDetailsLoadResult?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new TestSpineAssetGroupDetailsLoader
        {
            LoadHandler = _ => loadCompletion.Task
        };
        var coordinator = CreateCoordinator(context, loader);
        context.PrimaryPane.ViewMode = AssetPaneViewMode.SpineGroups;
        context.PrimaryPane.ReplaceSpineGroups("ui.ab", [entry], "empty");
        context.PrimaryPane.UpdateSpineGroupSelection(entry);

        var firstLoadTask = coordinator.HandlePrimaryPaneSelectionChangedAsync(
            isSynchronizingPrimaryPaneState: false,
            captureActiveTabState: static () => { });

        var groupTab = Assert.Single(context.BundleTab.SpineGroupTabs);
        Assert.False(firstLoadTask.IsCompleted);
        Assert.Equal(1, loader.LoadCallCount);

        var secondLoadTask = coordinator.ActivateSpineGroupDocumentTabAsync(groupTab);

        Assert.False(secondLoadTask.IsCompleted);
        Assert.Equal(1, loader.LoadCallCount);

        loadCompletion.SetResult(new SpineAssetGroupDetailsLoadResult(
            new SpineAssetGroupDetailsDto(groupTab.Item, [], [], groupTab.Item.Diagnostics),
            TexturePreview: null));
        await Task.WhenAll(firstLoadTask, secondLoadTask);

        Assert.Equal(1, loader.LoadCallCount);
        Assert.NotNull(groupTab.Details);
        Assert.False(groupTab.Inspector.Preview.IsPreviewLoading);
    }

    [Fact]
    public async Task HandlePrimaryPaneSelectionChangedAsync_ShouldRestoreSummaryPreview_WhenSpineDetailsLoadFails()
    {
        var context = CreateContext();
        var entry = CreateSpineGroupEntry("hero", "Hero");
        var loadCompletion = new TaskCompletionSource<SpineAssetGroupDetailsLoadResult?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new TestSpineAssetGroupDetailsLoader
        {
            LoadHandler = _ => loadCompletion.Task
        };
        var coordinator = CreateCoordinator(context, loader);
        context.PrimaryPane.ViewMode = AssetPaneViewMode.SpineGroups;
        context.PrimaryPane.ReplaceSpineGroups("ui.ab", [entry], "empty");
        context.PrimaryPane.UpdateSpineGroupSelection(entry);

        var loadTask = coordinator.HandlePrimaryPaneSelectionChangedAsync(
            isSynchronizingPrimaryPaneState: false,
            captureActiveTabState: static () => { });
        var groupTab = Assert.Single(context.BundleTab.SpineGroupTabs);

        loadCompletion.SetResult(null);
        await loadTask;

        Assert.False(groupTab.Inspector.Preview.IsPreviewLoading);
        Assert.Equal(Visibility.Visible, groupTab.Inspector.Preview.PreviewSummaryVisibility);
        Assert.Equal(
            ResourceKeys.InspectorSpineGroupSummaryFormat + ":1|1|1|0|0|0",
            groupTab.Inspector.Preview.PreviewText);
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
    public async Task SynchronizeInspectorWithActiveBundleTabAsync_ShouldPreserveActiveAssetDocument_WhenSelectedSpineGroupHasOpenTab()
    {
        var context = CreateContext();
        var coordinator = CreateCoordinator(context);
        var assetTab = context.HostCoordinator.CreateAssetDocumentTab(
            context.BundleTab.BundleId,
            new AssetId("asset-texture"),
            "Texture");
        var groupEntry = CreateSpineGroupEntry("hero", "Hero");
        var groupTab = context.HostCoordinator.CreateSpineGroupDocumentTab(groupEntry.Item);
        context.BundleTab.AddAssetTab(assetTab, activate: true);
        context.BundleTab.AddSpineGroupTab(groupTab, activate: false);
        context.PrimaryPane.ViewMode = AssetPaneViewMode.SpineGroups;
        context.PrimaryPane.ReplaceSpineGroups("ui.ab", [groupEntry], "empty");
        context.PrimaryPane.UpdateSpineGroupSelection(groupEntry);

        await coordinator.SynchronizeInspectorWithActiveBundleTabAsync();

        Assert.Same(assetTab, context.BundleTab.ActiveAssetTab);
        Assert.Same(assetTab, context.BundleTab.ActiveDocumentTab?.AssetTab);
        Assert.Same(assetTab.Inspector, context.HostCoordinator.CurrentInspector);
        Assert.NotSame(groupTab, context.BundleTab.ActiveSpineGroupTab);
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
    public async Task HandlePrimaryPaneSelectionChangedAsync_ShouldApplySpineAnimationPreview()
    {
        var context = CreateContext();
        var entry = CreateSpineGroupEntry("hero", "Hero");
        var texturePageAsset = entry.Item.TexturePageAssets[0];
        var atlasPage = new SpineAtlasPageDto(
            "hero.png",
            Width: 128,
            Height: 128,
            Regions:
            [
                new SpineAtlasRegionDto("body", "hero.png", 0, 0, 64, 64, IsRotated: false)
            ],
            TexturePageAsset: texturePageAsset);
        var animationPreview = CreateAnimationPreview(entry.Item.SkeletonAssets[0], atlasPage.Name);
        var diagnostics = new[]
        {
            new SpineAssetGroupDiagnosticDto(
                SpineAssetGroupDiagnosticKind.AnimationPreviewUnsupportedFeature,
                entry.Item.GroupKey,
                [entry.Item.SkeletonAssets[0]],
                AnimationUnsupportedFeature: SpineAnimationUnsupportedFeatureKind.MeshAttachment)
        };
        var loader = new TestSpineAssetGroupDetailsLoader
        {
            LoadHandler = tab => Task.FromResult<SpineAssetGroupDetailsLoadResult?>(new SpineAssetGroupDetailsLoadResult(
                new SpineAssetGroupDetailsDto(
                    tab.Item,
                    [],
                    [atlasPage],
                    diagnostics,
                    animationPreview),
                TexturePreview: null,
                AnimationTextures: new Dictionary<string, SpineAnimationPreviewTextureState>(StringComparer.Ordinal)
                {
                    [atlasPage.Name] = new SpineAnimationPreviewTextureState(
                        atlasPage.Name,
                        CreateTexturePreviewState(atlasPage, texturePageAsset, [1, 2, 3]))
                }))
        };
        var coordinator = CreateCoordinator(context, loader);
        context.PrimaryPane.ViewMode = AssetPaneViewMode.SpineGroups;
        context.PrimaryPane.ReplaceSpineGroups("ui.ab", [entry], "empty");
        context.PrimaryPane.UpdateSpineGroupSelection(entry);

        await coordinator.HandlePrimaryPaneSelectionChangedAsync(
            isSynchronizingPrimaryPaneState: false,
            captureActiveTabState: static () => { });

        var groupTab = Assert.Single(context.BundleTab.SpineGroupTabs);
        Assert.Equal(Visibility.Visible, groupTab.Inspector.Preview.PreviewSpineAnimationVisibility);
        Assert.Equal(Visibility.Collapsed, groupTab.Inspector.Preview.PreviewImageVisibility);
        Assert.True(groupTab.Inspector.Preview.SpineAnimation.HasSpineAnimation);
        Assert.Equal("idle", Assert.Single(groupTab.Inspector.Preview.SpineAnimation.AnimationNames));
        Assert.Contains(
            groupTab.Inspector.Preview.PreviewInfoRows,
            row => row.Label == ResourceKeys.InspectorSpineGroupAnimationPreviewLabel &&
                   row.Value == ResourceKeys.InspectorSpineGroupAnimationPreviewFormat + ":1|1|1");
        Assert.Contains(
            groupTab.Inspector.Preview.PreviewInfoRows,
            row => row.Label == ResourceKeys.InspectorSpineGroupRuntimeAdapterLabel &&
                   row.Value == ResourceKeys.InspectorSpineGroupRuntimeAdapterFormat + ":4.1|V41");
        Assert.Contains(
            groupTab.Inspector.Preview.PreviewInfoRows,
            row => row.Label == ResourceKeys.InspectorSpineGroupDiagnosticLabelFormat + ":1" &&
                   row.Value ==
                   ResourceKeys.InspectorSpineGroupDiagnosticWithDetailFormat + ":" +
                   ResourceKeys.InspectorSpineGroupDiagnosticFormat + ":" +
                   ResourceKeys.InspectorSpineGroupDiagnosticAnimationPreviewUnsupportedFeature + "|" +
                   entry.Item.GroupKey + "|1|" +
                   ResourceKeys.InspectorSpineGroupDiagnosticAnimationUnsupportedFeatureDetailFormat + ":" +
                   ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeatureMeshAttachment);
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

}
