using System.Collections.Concurrent;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
using static AssestBundleTools.Presentation.Tests.ViewModels.BundleExplorerPreviewTestSupport;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class BundleExplorerAssetPreviewSchedulingTests
{
    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldRunForegroundAndBackgroundPrewarmOnSeparateSlots()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var foregroundAssetId = new AssetId("asset-current");
        var backgroundAssetId = new AssetId("asset-background");
        var context = CreatePreviewContext(workspaceId, "PreviewPrioritySlots");
        var foregroundCanFinish = new TaskCompletionSource<Result<AssetPreviewDto>>();
        var backgroundCanFinish = new TaskCompletionSource<Result<AssetPreviewDto>>();
        var startedAssetIds = new List<AssetId>();
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetPreviewHandler = query =>
            {
                startedAssetIds.Add(query.AssetId);
                return query.AssetId.Equals(foregroundAssetId)
                    ? foregroundCanFinish.Task
                    : backgroundCanFinish.Task;
            }
        };
        var coordinator = CreateCoordinator(context, inspectionGateway, previewWorkerCount: 4);
        var foregroundTab = CreateAssetTab(bundleId, foregroundAssetId, context.LocalizationService);
        var backgroundTab = CreateAssetTab(bundleId, backgroundAssetId, context.LocalizationService);
        var foregroundDetails = CreateAssetDetails(workspaceId, bundleId, foregroundAssetId);
        var backgroundDetails = CreateAssetDetails(workspaceId, bundleId, backgroundAssetId);
        foregroundTab.ApplyDetails(foregroundDetails);
        backgroundTab.ApplyDetails(backgroundDetails);

        var foregroundLoad = coordinator.LoadInspectorPreviewAsync(
            foregroundTab,
            foregroundDetails,
            static _ => "failed",
            () => true,
            AssetPreviewLoadPriority.Foreground);
        var backgroundLoad = coordinator.LoadInspectorPreviewAsync(
            backgroundTab,
            backgroundDetails,
            static _ => "failed",
            () => true,
            AssetPreviewLoadPriority.BackgroundPrewarm);
        await WaitUntilAsync(() => startedAssetIds.Count == 2);

        Assert.Contains(foregroundAssetId, startedAssetIds);
        Assert.Contains(backgroundAssetId, startedAssetIds);

        foregroundCanFinish.SetResult(CreateSummaryPreviewResult(CreatePreviewQuery(
            context,
            workspaceId,
            bundleId,
            foregroundAssetId)));
        backgroundCanFinish.SetResult(CreateSummaryPreviewResult(CreatePreviewQuery(
            context,
            workspaceId,
            bundleId,
            backgroundAssetId)));
        await Task.WhenAll(foregroundLoad, backgroundLoad);
    }

    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldSkipBackgroundPrewarm_WhenOnlyForegroundSlotExists()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-background");
        var context = CreatePreviewContext(workspaceId, "PreviewSingleSlot");
        var previewQueryCount = 0;
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetPreviewHandler = query =>
            {
                previewQueryCount++;
                return Task.FromResult(CreateSummaryPreviewResult(query));
            }
        };
        var coordinator = CreateCoordinator(context, inspectionGateway, previewWorkerCount: 1);
        var assetTab = CreateAssetTab(bundleId, assetId, context.LocalizationService);
        var details = CreateAssetDetails(workspaceId, bundleId, assetId);
        assetTab.ApplyDetails(details);

        await coordinator.LoadInspectorPreviewAsync(
            assetTab,
            details,
            static _ => "failed",
            () => true,
            AssetPreviewLoadPriority.BackgroundPrewarm);

        Assert.Equal(0, previewQueryCount);
        Assert.False(assetTab.Inspector.Preview.IsPreviewLoading);
        Assert.Equal(string.Empty, assetTab.Inspector.Preview.PreviewText);
    }

    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldShowForegroundPreviewInShellStatus()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-current");
        var context = CreatePreviewContext(workspaceId, "PreviewStatusBar");
        var statusService = new RecordingBackgroundTaskStatusService();
        var scheduler = new BackgroundWorkScheduler(
            new TestBackgroundTaskRunner(),
            statusService,
            previewWorkerCount: 2);
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetPreviewHandler = query => Task.FromResult(CreateSummaryPreviewResult(query))
        };
        var coordinator = new BundleExplorerAssetPreviewCoordinator(
            inspectionGateway,
            context.WorkspaceContextService,
            context.WorkspaceShell,
            context.AppLogService,
            context.LocalizationService,
            new TestBackgroundTaskRunner(),
            previewWorkerCount: 2,
            backgroundWorkScheduler: scheduler);
        var assetTab = CreateAssetTab(bundleId, assetId, context.LocalizationService);
        var details = CreateAssetDetails(workspaceId, bundleId, assetId);
        assetTab.ApplyDetails(details);

        await coordinator.LoadInspectorPreviewAsync(
            assetTab,
            details,
            static _ => "failed",
            () => true,
            AssetPreviewLoadPriority.Foreground);

        var task = Assert.Single(statusService.Tasks);
        Assert.Equal($"{ResourceKeys.BundleExplorerStatusLoadPreviewFormat}:Button", task.Title);
        Assert.Null(task.TotalCount);
        Assert.True(task.IsDisposed);
    }

    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldKeepBackgroundPrewarmOutOfShellStatus()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-background");
        var context = CreatePreviewContext(workspaceId, "PreviewPrewarmStatusBar");
        var statusService = new RecordingBackgroundTaskStatusService();
        var scheduler = new BackgroundWorkScheduler(
            new TestBackgroundTaskRunner(),
            statusService,
            previewWorkerCount: 2);
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetPreviewHandler = query => Task.FromResult(CreateSummaryPreviewResult(query))
        };
        var coordinator = new BundleExplorerAssetPreviewCoordinator(
            inspectionGateway,
            context.WorkspaceContextService,
            context.WorkspaceShell,
            context.AppLogService,
            context.LocalizationService,
            new TestBackgroundTaskRunner(),
            previewWorkerCount: 2,
            backgroundWorkScheduler: scheduler);
        var assetTab = CreateAssetTab(bundleId, assetId, context.LocalizationService);
        var details = CreateAssetDetails(workspaceId, bundleId, assetId);
        assetTab.ApplyDetails(details);

        await coordinator.LoadInspectorPreviewAsync(
            assetTab,
            details,
            static _ => "failed",
            () => true,
            AssetPreviewLoadPriority.BackgroundPrewarm);

        Assert.Empty(statusService.Tasks);
    }

    [Fact]
    public async Task LoadSpineGroupDetailsAsync_ShouldShowForegroundPreviewInShellStatus()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-spine");
        var context = CreatePreviewContext(workspaceId, "SpinePreviewStatusBar");
        var statusService = new RecordingBackgroundTaskStatusService();
        var scheduler = new BackgroundWorkScheduler(
            new TestBackgroundTaskRunner(),
            statusService,
            previewWorkerCount: 2);
        var group = new SpineAssetGroupDto(
            GroupId: "spine-group",
            BundleId: bundleId,
            DisplayName: "Amiya Spine",
            GroupKey: "spine-group-key",
            SkeletonAssets: Array.Empty<SpineAssetGroupPartDto>(),
            AtlasAssets: Array.Empty<SpineAssetGroupPartDto>(),
            TexturePageAssets: Array.Empty<SpineAssetGroupPartDto>(),
            Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>());
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            LoadSpineAssetGroupDetailsHandler = _ => Task.FromResult(Result<SpineAssetGroupDetailsDto>.Success(
                new SpineAssetGroupDetailsDto(
                    group,
                    Array.Empty<SpineSkeletonSummaryDto>(),
                    Array.Empty<SpineAtlasPageDto>(),
                    Array.Empty<SpineAssetGroupDiagnosticDto>())))
        };
        var loader = new SpineAssetGroupDetailsLoader(
            inspectionGateway,
            context.WorkspaceContextService,
            context.WorkspaceShell,
            new TestBackgroundTaskRunner(),
            scheduler,
            context.LocalizationService);
        var groupTab = new SpineAssetGroupDocumentTabViewModel(
            group,
            InspectorInformationTextSetFactory.Create(context.LocalizationService));

        var result = await loader.LoadAsync(groupTab);

        Assert.NotNull(result);
        Assert.NotEmpty(statusService.Tasks);
        var task = statusService.Tasks[0];
        Assert.Equal($"{ResourceKeys.BundleExplorerStatusLoadPreviewFormat}:Amiya Spine", task.Title);
        Assert.All(statusService.Tasks, static task => Assert.True(task.IsDisposed));
    }

    [Fact]
    public async Task LoadSpineGroupDetailsAsync_ShouldUseInteractivePreviewQuery()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-spine");
        var context = CreatePreviewContext(workspaceId, "SpinePreviewFastPath");
        var group = CreateSpineGroup(bundleId);
        var queries = new List<LoadSpineAssetGroupDetailsQuery>();
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            LoadSpineAssetGroupDetailsHandler = query =>
            {
                queries.Add(query);
                return Task.FromResult(Result<SpineAssetGroupDetailsDto>.Success(
                    new SpineAssetGroupDetailsDto(
                        group,
                        Array.Empty<SpineSkeletonSummaryDto>(),
                        Array.Empty<SpineAtlasPageDto>(),
                        Array.Empty<SpineAssetGroupDiagnosticDto>())));
            }
        };
        var loader = new SpineAssetGroupDetailsLoader(
            inspectionGateway,
            context.WorkspaceContextService,
            context.WorkspaceShell,
            new TestBackgroundTaskRunner(),
            localizationService: context.LocalizationService);
        var groupTab = new SpineAssetGroupDocumentTabViewModel(
            group,
            InspectorInformationTextSetFactory.Create(context.LocalizationService));

        var result = await loader.LoadAsync(groupTab);

        Assert.NotNull(result);
        Assert.NotEmpty(queries);
        var query = queries[0];
        Assert.Equal(SpineAssetGroupDetailsLoadMode.InteractivePreview, query.LoadMode);
    }

    [Fact]
    public async Task LoadSpineGroupDetailsAsync_ShouldLoadInteractiveDiagnostics_WhenAnimationHasNoTexturePayload()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-spine");
        var context = CreatePreviewContext(workspaceId, "SpinePreviewDiagnostics");
        var group = CreateSpineGroup(bundleId);
        var queries = new List<LoadSpineAssetGroupDetailsQuery>();
        var texturePreviewQueryCount = 0;
        var previewDetails = new SpineAssetGroupDetailsDto(
            group,
            Array.Empty<SpineSkeletonSummaryDto>(),
            [
                new SpineAtlasPageDto(
                    "missing.png",
                    64,
                    64,
                    TexturePageAsset: null,
                    Regions: Array.Empty<SpineAtlasRegionDto>())
            ],
            Array.Empty<SpineAssetGroupDiagnosticDto>(),
            CreateMinimalAnimationPreview(group.SkeletonAssets[0]));
        var diagnosticsDetails = previewDetails with
        {
            Diagnostics =
            [
                new SpineAssetGroupDiagnosticDto(
                    SpineAssetGroupDiagnosticKind.AtlasPageTextureUnmatched,
                    group.GroupKey,
                    group.AtlasAssets,
                    AtlasPageName: "missing.png")
            ]
        };
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetPreviewHandler = _ =>
            {
                texturePreviewQueryCount++;
                throw new InvalidOperationException("Initial Spine diagnostics should not request texture previews.");
            },
            LoadSpineAssetGroupDetailsHandler = query =>
            {
                queries.Add(query);
                return Task.FromResult(Result<SpineAssetGroupDetailsDto>.Success(
                    query.LoadMode == SpineAssetGroupDetailsLoadMode.InteractiveDiagnostics
                        ? diagnosticsDetails
                        : previewDetails));
            }
        };
        var loader = new SpineAssetGroupDetailsLoader(
            inspectionGateway,
            context.WorkspaceContextService,
            context.WorkspaceShell,
            new TestBackgroundTaskRunner(),
            localizationService: context.LocalizationService);
        var groupTab = new SpineAssetGroupDocumentTabViewModel(
            group,
            InspectorInformationTextSetFactory.Create(context.LocalizationService));

        var result = await loader.LoadAsync(groupTab);

        Assert.NotNull(result);
        Assert.Equal(
            [SpineAssetGroupDetailsLoadMode.InteractivePreview, SpineAssetGroupDetailsLoadMode.InteractiveDiagnostics],
            queries.Select(static query => query.LoadMode).ToArray());
        Assert.Contains(
            result!.Details.Diagnostics,
            static diagnostic => diagnostic.Kind == SpineAssetGroupDiagnosticKind.AtlasPageTextureUnmatched);
        Assert.Equal(0, texturePreviewQueryCount);
    }

    [Fact]
    public async Task PrewarmBackgroundPreviews_ShouldPrioritizeRecentlyActivatedTabs()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var oldAssetId = new AssetId("asset-old");
        var recentAssetId = new AssetId("asset-recent");
        var foregroundAssetId = new AssetId("asset-foreground");
        var context = CreatePreviewContext(workspaceId, "PreviewPrewarmRecent");
        var previewQueryAssetIds = new ConcurrentQueue<AssetId>();
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetPreviewHandler = query =>
            {
                previewQueryAssetIds.Enqueue(query.AssetId);
                return Task.FromResult(CreateSummaryPreviewResult(query));
            }
        };
        var previewCoordinator = CreateCoordinator(context, inspectionGateway, previewWorkerCount: 2);
        AssetDocumentTabViewModel? activeAssetTab = null;
        var loadCoordinator = new BundleExplorerAssetPreviewLoadCoordinator(
            previewCoordinator,
            () => activeAssetTab,
            static _ => "failed");
        var oldTab = CreateAssetTab(bundleId, oldAssetId, context.LocalizationService);
        var recentTab = CreateAssetTab(bundleId, recentAssetId, context.LocalizationService);
        var foregroundTab = CreateAssetTab(bundleId, foregroundAssetId, context.LocalizationService);
        oldTab.ApplyDetails(CreateAssetDetails(workspaceId, bundleId, oldAssetId));
        recentTab.ApplyDetails(CreateAssetDetails(workspaceId, bundleId, recentAssetId));
        foregroundTab.ApplyDetails(CreateAssetDetails(workspaceId, bundleId, foregroundAssetId));

        oldTab.Inspector.ShowPreviewMessage("loaded");
        activeAssetTab = oldTab;
        loadCoordinator.LoadForegroundPreview(oldTab, promoteLoadingPreview: false);
        recentTab.Inspector.ShowPreviewMessage("loaded");
        activeAssetTab = recentTab;
        loadCoordinator.LoadForegroundPreview(recentTab, promoteLoadingPreview: false);
        oldTab.Inspector.Preview.ResetState();
        recentTab.Inspector.Preview.ResetState();

        activeAssetTab = foregroundTab;
        loadCoordinator.PrewarmBackgroundPreviews(
            [oldTab, recentTab, foregroundTab],
            foregroundTab);
        await WaitUntilAsync(() => previewQueryAssetIds.Count == 2);

        Assert.Equal([recentAssetId, oldAssetId], previewQueryAssetIds.ToArray());
    }

    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldRetreatBackgroundPrewarmForSameBundle_WhenPreviewRunsOutOfMemory()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-memory");
        var outOfMemoryAssetId = new AssetId("asset-oom");
        var skippedAssetId = new AssetId("asset-skipped");
        var foregroundAssetId = new AssetId("asset-foreground");
        var resumedAssetId = new AssetId("asset-resumed");
        var context = CreatePreviewContext(workspaceId, "PreviewPrewarmMemoryRetreat");
        var previewQueryAssetIds = new List<AssetId>();
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetPreviewHandler = query =>
            {
                previewQueryAssetIds.Add(query.AssetId);
                return Task.FromResult(query.AssetId.Equals(outOfMemoryAssetId)
                    ? CreateOutOfMemoryPreviewResult(query)
                    : CreateSummaryPreviewResult(query));
            }
        };
        var coordinator = CreateCoordinator(context, inspectionGateway, previewWorkerCount: 2);
        var outOfMemoryTab = CreateAssetTab(bundleId, outOfMemoryAssetId, context.LocalizationService);
        var skippedTab = CreateAssetTab(bundleId, skippedAssetId, context.LocalizationService);
        var foregroundTab = CreateAssetTab(bundleId, foregroundAssetId, context.LocalizationService);
        var resumedTab = CreateAssetTab(bundleId, resumedAssetId, context.LocalizationService);
        var outOfMemoryDetails = CreateAssetDetails(workspaceId, bundleId, outOfMemoryAssetId);
        var skippedDetails = CreateAssetDetails(workspaceId, bundleId, skippedAssetId);
        var foregroundDetails = CreateAssetDetails(workspaceId, bundleId, foregroundAssetId);
        var resumedDetails = CreateAssetDetails(workspaceId, bundleId, resumedAssetId);
        outOfMemoryTab.ApplyDetails(outOfMemoryDetails);
        skippedTab.ApplyDetails(skippedDetails);
        foregroundTab.ApplyDetails(foregroundDetails);
        resumedTab.ApplyDetails(resumedDetails);

        await coordinator.LoadInspectorPreviewAsync(
            outOfMemoryTab,
            outOfMemoryDetails,
            static _ => "failed",
            () => true,
            AssetPreviewLoadPriority.BackgroundPrewarm);
        await coordinator.LoadInspectorPreviewAsync(
            skippedTab,
            skippedDetails,
            static _ => "failed",
            () => true,
            AssetPreviewLoadPriority.BackgroundPrewarm);
        await coordinator.LoadInspectorPreviewAsync(
            foregroundTab,
            foregroundDetails,
            static _ => "failed",
            () => true,
            AssetPreviewLoadPriority.Foreground);
        await coordinator.LoadInspectorPreviewAsync(
            resumedTab,
            resumedDetails,
            static _ => "failed",
            () => true,
            AssetPreviewLoadPriority.BackgroundPrewarm);

        Assert.Equal(
            [outOfMemoryAssetId, foregroundAssetId, resumedAssetId],
            previewQueryAssetIds);
        Assert.False(skippedTab.Inspector.Preview.IsPreviewLoading);
        Assert.Equal(string.Empty, skippedTab.Inspector.Preview.PreviewText);
    }

    private static PreviewContext CreatePreviewContext(WorkspaceId workspaceId, string workspaceName)
    {
        var localizationService = new TestLocalizationService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest(workspaceName);
        var workspaceSummary = CreateWorkspaceSummary(workspaceId, workspaceRequest);
        workspaceContextService.RegisterOrActivateWorkspace(
            workspaceSummary,
            workspaceRequest.WorkspaceDataRootPath,
            workspaceRequest.AssetBundleDecryptorId);
        var workspaceShell = new BundleExplorerWorkspaceShellViewModel(
            localizationService,
            workspaceContextService,
            new TestAssetBundleDecryptorRegistry());
        workspaceShell.ApplyWorkspace(workspaceSummary, workspaceRequest);

        return new PreviewContext(
            LocalizationService: localizationService,
            AppLogService: new TestAppLogService(),
            WorkspaceContextService: workspaceContextService,
            WorkspaceShell: workspaceShell,
            WorkspaceRequest: workspaceRequest);
    }

    private static BundleExplorerAssetPreviewCoordinator CreateCoordinator(
        PreviewContext context,
        TestWorkspaceAssetInspectionGateway inspectionGateway,
        int previewWorkerCount)
    {
        return new BundleExplorerAssetPreviewCoordinator(
            inspectionGateway,
            context.WorkspaceContextService,
            context.WorkspaceShell,
            context.AppLogService,
            context.LocalizationService,
            new TestBackgroundTaskRunner(),
            previewWorkerCount: previewWorkerCount);
    }

    private static GetAssetPreviewQuery CreatePreviewQuery(
        PreviewContext context,
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId)
    {
        return new GetAssetPreviewQuery(
            workspaceId,
            WorkspaceCatalogKeyFactory.CreateWorkspaceKey(
                context.WorkspaceRequest.SourcePath,
                context.WorkspaceRequest.WorkspaceDataRootPath),
            context.WorkspaceRequest.WorkspaceDataRootPath,
            bundleId,
            assetId);
    }

    private static Result<AssetPreviewDto> CreateOutOfMemoryPreviewResult(GetAssetPreviewQuery query)
    {
        return Result<AssetPreviewDto>.Success(new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.Summary,
            ContentType: "text/plain",
            TextContent: null,
            BinaryContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreviewMessage: new AssetPreviewMessageDto(AssetPreviewMessageKey.OutOfMemory),
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.OutOfMemory)));
    }

    private static SpineAssetGroupDto CreateSpineGroup(BundleId bundleId)
    {
        var skeleton = new SpineAssetGroupPartDto(
            Role: SpineAssetGroupPartRole.Skeleton,
            BundleId: bundleId,
            AssetId: new AssetId("skeleton"),
            BundleDisplayName: "spine.ab",
            Name: "skeleton.json",
            AssetPath: "Assets/Spine/skeleton.json",
            AssetType: AssetType.TextAsset,
            AssetTypeDisplayName: null);
        var atlas = new SpineAssetGroupPartDto(
            Role: SpineAssetGroupPartRole.Atlas,
            BundleId: bundleId,
            AssetId: new AssetId("atlas"),
            BundleDisplayName: "spine.ab",
            Name: "skeleton.atlas",
            AssetPath: "Assets/Spine/skeleton.atlas",
            AssetType: AssetType.TextAsset,
            AssetTypeDisplayName: null);
        return new SpineAssetGroupDto(
            GroupId: "spine-group",
            BundleId: bundleId,
            DisplayName: "Amiya Spine",
            GroupKey: "spine-group-key",
            SkeletonAssets: [skeleton],
            AtlasAssets: [atlas],
            TexturePageAssets: Array.Empty<SpineAssetGroupPartDto>(),
            Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>());
    }

    private static SpineAnimationPreviewDto CreateMinimalAnimationPreview(SpineAssetGroupPartDto skeletonAsset)
    {
        var runtime = new SpineAnimationRuntimeDto(
            "4.2.74",
            Major: 4,
            Minor: 2,
            AdapterKey: "V42",
            SpineAnimationRuntimeSupportKind.OfficialAdapterAvailable);
        var animation = new SpineAnimationClipDto(
            "idle",
            Duration: 1d,
            BoneTimelines: Array.Empty<SpineAnimationBoneTimelineDto>(),
            SlotAttachmentTimelines: Array.Empty<SpineAnimationSlotAttachmentTimelineDto>());
        return new SpineAnimationPreviewDto(
            skeletonAsset,
            "4.2.74",
            Bones:
            [
                new SpineAnimationBoneDto(
                    "root",
                    ParentName: null,
                    X: 0d,
                    Y: 0d,
                    Rotation: 0d,
                    ScaleX: 1d,
                    ScaleY: 1d)
            ],
            Slots:
            [
                new SpineAnimationSlotDto(
                    "body",
                    "root",
                    SetupAttachmentName: null,
                    DrawOrder: 0)
            ],
            Attachments: Array.Empty<SpineAnimationAttachmentDto>(),
            Animations: [animation],
            runtime)
        {
            PlaybackSource = new SpineAnimationRuntimePlaybackSourceDto(
                "spine-group-key",
                skeletonAsset,
                SkeletonJson: "{}",
                runtime,
                AtlasPages: Array.Empty<SpineAtlasPageDto>(),
                AtlasTexts: Array.Empty<string>())
        };
    }

    private sealed record PreviewContext(
        TestLocalizationService LocalizationService,
        TestAppLogService AppLogService,
        TestWorkspaceContextService WorkspaceContextService,
        BundleExplorerWorkspaceShellViewModel WorkspaceShell,
        WorkspaceNavigationRequest WorkspaceRequest);

    private sealed class RecordingBackgroundTaskStatusService : IAppBackgroundTaskStatusService
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public List<RecordingBackgroundTaskHandle> Tasks { get; } = [];

        public string StatusText => string.Empty;

        public string ProgressText => string.Empty;

        public double ProgressValue => 0d;

        public bool IsProgressIndeterminate => false;

        public Microsoft.UI.Xaml.Visibility ProgressVisibility => Microsoft.UI.Xaml.Visibility.Collapsed;

        public string StatusGlyph => string.Empty;

        public IAppBackgroundTaskHandle BeginTask(string title, int? totalCount = null)
        {
            var task = new RecordingBackgroundTaskHandle(title, totalCount);
            Tasks.Add(task);
            return task;
        }
    }

    private sealed class RecordingBackgroundTaskHandle : IAppBackgroundTaskHandle
    {
        public RecordingBackgroundTaskHandle(string title, int? totalCount)
        {
            Title = title;
            TotalCount = totalCount;
        }

        public string Title { get; }

        public int? TotalCount { get; }

        public bool IsDisposed { get; private set; }

        public void ReportProgress(int completedCount, int totalCount, string? detail = null)
        {
        }

        public void ReportIndeterminate(string? detail = null)
        {
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
