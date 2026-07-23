using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Queries;
using AssestBundleTools.Application.Extraction.Services;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Extraction;

internal static partial class ExtractionWorkflowHandlerTests
{
    public static async Task GetWorkspaceBatchExtractionPreviewHandler_ShouldIncludeSpineGroups_WhenAllCategoryIsSelected()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.char_1001_amiya2");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        RegisterSpineGroupAssets(workspaceId, bundleId, registry);
        var handler = new GetWorkspaceBatchExtractionPreviewHandler(
            registry,
            new LoadSpineAssetGroupsHandler(registry));

        var result = await handler.HandleAsync(new GetWorkspaceBatchExtractionPreviewQuery(
            workspaceId,
            OutputDirectory: @"D:\Out",
            SelectedBundleIds: new[] { bundleId },
            SelectedCategories: new[] { WorkspaceBatchExtractionCategoryDto.All }));

        TestAssert.True(result.IsSuccess, "Workspace batch preview should succeed with Spine planning enabled.");
        TestAssert.Equal(4, result.Value?.CandidateAssetCount ?? 0, "All should include regular asset candidates.");
        TestAssert.Equal(1, result.Value?.CandidateSpineGroupCount ?? 0, "All should include Spine group candidates.");
        var bundlePlan = TestAssert.Single(result.Value?.BundlePlans ?? Array.Empty<WorkspaceBatchExtractionBundlePlanDto>());
        TestAssert.Equal(4, bundlePlan.AssetCount, "Bundle plan should keep the regular asset count.");
        TestAssert.Equal(1, bundlePlan.SpineGroupCount, "Bundle plan should keep the Spine group count.");
        TestAssert.True(
            (result.Value?.CategoryPlans ?? Array.Empty<WorkspaceBatchExtractionCategoryPlanDto>())
                .Any(static plan =>
                    plan.Category == WorkspaceBatchExtractionCategoryDto.SpineGroup &&
                    plan.CandidateCount == 1),
            "Category plans should expose Spine group candidates for the selected bundles.");
    }

    public static async Task GetWorkspaceBatchExtractionPreviewHandler_ShouldPassWorkspaceDataRootPath_ToSpinePlanner()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.char_1001_amiya2");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        RegisterSpineGroupAssets(workspaceId, bundleId, registry);
        var spineGroupsHandler = new RecordingSpineGroupListHandler();
        var handler = new GetWorkspaceBatchExtractionPreviewHandler(registry, spineGroupsHandler);

        var result = await handler.HandleAsync(new GetWorkspaceBatchExtractionPreviewQuery(
            workspaceId,
            OutputDirectory: @"D:\Out",
            WorkspaceDataRootPath: WorkspaceDataRootPath,
            SelectedBundleIds: new[] { bundleId },
            SelectedCategories: new[] { WorkspaceBatchExtractionCategoryDto.SpineGroup }));

        TestAssert.True(result.IsSuccess, "Workspace batch preview should succeed with a recording Spine planner.");
        var query = TestAssert.Single(spineGroupsHandler.Queries);
        TestAssert.Equal(WorkspaceDataRootPath, query.WorkspaceDataRootPath, "Spine preview planning should use the workspace data root so persistent Spine catalogs can be reused.");
    }

    public static async Task ExtractWorkspaceAssetsHandler_ShouldExtractSpineGroups_WhenAllCategoryIsSelected()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.char_1001_amiya2");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        var parts = RegisterSpineGroupAssets(workspaceId, bundleId, registry);
        var previewHandler = new TestAssetPreviewHandler();
        RegisterSpineGroupPreviews(workspaceId, bundleId, parts, previewHandler);
        var fileStore = new TestAssetExtractionFileStore();
        var changeStore = new TestWorkspaceChangeStore();
        var singleHandler = CreateHandler(registry, previewHandler, fileStore, changeStore);
        var recorder = new WorkspaceExtractionOperationRecorder(
            new TestWorkspaceDataStoreFactory(changeStore),
            new WorkspaceOperationLogService());
        var batchHandler = new ExtractWorkspaceAssetsHandler(
            registry,
            singleHandler,
            recorder,
            new LoadSpineAssetGroupsHandler(registry),
            new ExtractSpineAssetGroupHandler(
                registry,
                new LoadSpineAssetGroupDetailsHandler(registry, previewHandler),
                singleHandler,
                recorder,
                new LoadSpineAssetGroupsHandler(registry)));

        var result = await batchHandler.HandleAsync(new ExtractWorkspaceAssetsCommand(
            workspaceId,
            OutputDirectory: @"D:\Out",
            WorkspaceKey: WorkspaceKey,
            WorkspaceDataRootPath: WorkspaceDataRootPath,
            SelectedBundleIds: new[] { bundleId },
            SelectedCategories: new[] { WorkspaceBatchExtractionCategoryDto.All }));

        TestAssert.True(result.IsSuccess, "All-category workspace extraction should succeed with Spine groups.");
        TestAssert.Equal(1, result.Value?.ExtractedSpineGroupCount ?? 0, "All should execute planned Spine group extraction.");
        TestAssert.Equal(1, result.Value?.SpineGroupResults?.Count ?? 0, "Result should expose Spine group extraction details.");
        TestAssert.True(
            fileStore.Requests.Any(static request => request.SuggestedOutputPath.Contains(@"bundle.char_1001_amiya2.ab\Spine\char_1001_amiya2", StringComparison.OrdinalIgnoreCase)),
            "Spine group output should be nested under the bundle Spine directory and group directory.");
    }

    public static async Task ExtractWorkspaceAssetsHandler_ShouldLimitExportManifest_ToSelectedSpineGroups()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var selectedBundleId = new BundleId("bundle.char_1001_amiya2");
        var skippedBundleId = new BundleId("bundle.char_1001_amiya3");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        var selectedParts = RegisterSpineGroupAssets(workspaceId, selectedBundleId, registry);
        RegisterSpineGroupAssets(workspaceId, skippedBundleId, registry);
        var previewHandler = new TestAssetPreviewHandler();
        RegisterSpineGroupPreviews(workspaceId, selectedBundleId, selectedParts, previewHandler);
        var fileStore = new TestAssetExtractionFileStore();
        var changeStore = new TestWorkspaceChangeStore();
        var manifestStore = new TestWorkspaceExportManifestStore();
        var singleHandler = CreateHandler(registry, previewHandler, fileStore, changeStore);
        var recorder = new WorkspaceExtractionOperationRecorder(
            new TestWorkspaceDataStoreFactory(changeStore),
            new WorkspaceOperationLogService());
        var batchHandler = new ExtractWorkspaceAssetsHandler(
            registry,
            singleHandler,
            recorder,
            new LoadSpineAssetGroupsHandler(registry),
            new ExtractSpineAssetGroupHandler(
                registry,
                new LoadSpineAssetGroupDetailsHandler(registry, previewHandler),
                singleHandler,
                recorder,
                new LoadSpineAssetGroupsHandler(registry)),
            exportManifestStore: manifestStore);
        const string selectedGroupId = "spine-group:bundle.char_1001_amiya2:assets/characters/char_1001_amiya2";

        var result = await batchHandler.HandleAsync(new ExtractWorkspaceAssetsCommand(
            workspaceId,
            OutputDirectory: @"D:\Out",
            WorkspaceKey: WorkspaceKey,
            WorkspaceDataRootPath: WorkspaceDataRootPath,
            SelectedBundleIds: new[] { selectedBundleId, skippedBundleId },
            SelectedCategories: new[] { WorkspaceBatchExtractionCategoryDto.SpineGroup },
            SelectedSpineGroupIds: new[] { selectedGroupId }));

        TestAssert.True(result.IsSuccess, "Selected Spine group batch extraction should succeed.");
        TestAssert.Equal(1, result.Value?.ExtractedSpineGroupCount ?? 0, "Only the selected Spine group should be extracted.");
        TestAssert.Equal(4, fileStore.Requests.Count, "Only the selected Spine group parts should write output files.");
        TestAssert.True(
            fileStore.Requests.All(static request => request.SuggestedOutputPath.Contains("bundle.char_1001_amiya2.ab", StringComparison.OrdinalIgnoreCase)),
            "Output files should belong to the selected Spine group bundle.");

        var manifestItems = manifestStore.LastRequest?.Items ?? Array.Empty<WorkspaceExportManifestSourceItemDto>();
        TestAssert.Equal(4, manifestItems.Count, "Manifest should include the selected Spine group parts.");
        TestAssert.True(
            manifestItems.All(static item => item.SpineGroupId == selectedGroupId),
            "Manifest should only include the selected Spine group id.");
        TestAssert.True(
            manifestItems.All(static item => item.OutputRole != WorkspaceExportManifestOutputRole.Asset),
            "Spine group manifest entries should keep their group output roles.");
    }

    public static async Task ExtractWorkspaceAssetsHandler_ShouldPassWorkspaceDataRootPath_ToSpinePlanner()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.char_1001_amiya2");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        RegisterSpineGroupAssets(workspaceId, bundleId, registry);
        var spineGroupsHandler = new RecordingSpineGroupListHandler();
        var batchHandler = new ExtractWorkspaceAssetsHandler(
            registry,
            loadSpineAssetGroupsHandler: spineGroupsHandler);

        var result = await batchHandler.HandleAsync(new ExtractWorkspaceAssetsCommand(
            workspaceId,
            OutputDirectory: @"D:\Out",
            WorkspaceKey: WorkspaceKey,
            WorkspaceDataRootPath: WorkspaceDataRootPath,
            SelectedBundleIds: new[] { bundleId },
            SelectedCategories: new[] { WorkspaceBatchExtractionCategoryDto.SpineGroup }));

        TestAssert.True(result.IsFailure, "A plan with Spine candidates but no Spine extractor should fail after planning.");
        var query = TestAssert.Single(spineGroupsHandler.Queries);
        TestAssert.Equal(WorkspaceDataRootPath, query.WorkspaceDataRootPath, "Workspace batch extraction should use the workspace data root while planning Spine groups.");
    }

    public static async Task ExtractSpineAssetGroupHandler_ShouldExtractSkeletonAtlasTextureAndAlphaPages()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.char_1001_amiya2");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        var parts = RegisterSpineGroupAssets(workspaceId, bundleId, registry);
        var previewHandler = new TestAssetPreviewHandler();
        RegisterSpineGroupPreviews(workspaceId, bundleId, parts, previewHandler);
        var fileStore = new TestAssetExtractionFileStore();
        var changeStore = new TestWorkspaceChangeStore();
        var singleHandler = CreateHandler(registry, previewHandler, fileStore, changeStore);
        var detailsHandler = new LoadSpineAssetGroupDetailsHandler(registry, previewHandler);
        var recorder = new WorkspaceExtractionOperationRecorder(
            new TestWorkspaceDataStoreFactory(changeStore),
            new WorkspaceOperationLogService());
        var handler = new ExtractSpineAssetGroupHandler(registry, detailsHandler, singleHandler, recorder);

        var result = await handler.HandleAsync(new ExtractSpineAssetGroupCommand(
            workspaceId,
            WorkspaceKey,
            WorkspaceDataRootPath,
            bundleId,
            "spine-group:bundle.char_1001_amiya2:assets/characters/char_1001_amiya2",
            @"D:\Selected"));

        TestAssert.True(result.IsSuccess, "Spine group extraction should succeed.");
        TestAssert.Equal(4, result.Value?.ExtractedAssetCount ?? 0, "Spine group extraction should write the complete asset set.");
        TestAssert.Equal(0, result.Value?.FailedAssetCount ?? -1, "Spine group extraction should not report failures.");

        var requestedPaths = fileStore.Requests
            .Select(static request => request.SuggestedOutputPath)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        TestAssert.Equal(4, requestedPaths.Length, "The exporter should receive four write requests.");
        TestAssert.True(
            requestedPaths.All(path => path.Contains(@"D:\Selected\char_1001_amiya2", StringComparison.OrdinalIgnoreCase)),
            "All Spine group parts should be written into a dedicated group folder.");
        TestAssert.True(
            requestedPaths.Any(static path => path.EndsWith("char_1001_amiya2.skel", StringComparison.OrdinalIgnoreCase)),
            "The default skeleton export should use the uniform .skel extension.");
        TestAssert.True(
            requestedPaths.Any(static path => path.EndsWith("char_1001_amiya2.atlas", StringComparison.OrdinalIgnoreCase)),
            "The atlas text should keep its atlas extension.");
        TestAssert.True(
            requestedPaths.Any(static path => path.EndsWith("char_1001_amiya2.png", StringComparison.OrdinalIgnoreCase)),
            "The color texture page should be exported as PNG.");
        TestAssert.True(
            requestedPaths.Any(static path => path.EndsWith("char_1001_amiya2_alpha.png", StringComparison.OrdinalIgnoreCase)),
            "The independent alpha texture page should be exported as PNG.");

        var operations = changeStore.SavedChangeSet?.Operations ?? Array.Empty<WorkspaceOperationRecordDto>();
        TestAssert.Equal(5, operations.Count, "Each Spine group part should be recorded plus one group summary operation.");
        TestAssert.True(
            operations.Take(4).All(static operation => operation.OperationKind == WorkspaceOperationKind.ExtractAsset),
            "Spine group extraction should reuse existing asset extraction operation records.");
        TestAssert.Equal(WorkspaceOperationKind.ExtractBatch, operations[^1].OperationKind, "Spine group summary should be appended after part operations.");
        TestAssert.Equal("SpineAssetGroup", operations[^1].Target?.AssetType, "Spine group summary should keep a stable group target type.");
        TestAssert.Equal(4, operations[^1].Result?.CompletedCount ?? 0, "Spine group summary should record completed part count.");
        TestAssert.Equal(0, operations[^1].Result?.FailedCount ?? -1, "Spine group summary should record failed part count.");
        TestAssert.Equal(4, operations[^1].Result?.AffectedAssetIds?.Count ?? 0, "Spine group summary should list affected part asset ids.");
    }

    public static async Task ExtractSpineAssetGroupHandler_ShouldUseJsonExtension_ForJsonSkeleton_WhenPreferenceEnabled()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.char_1001_amiya2");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        var parts = RegisterSpineGroupAssets(workspaceId, bundleId, registry);
        var previewHandler = new TestAssetPreviewHandler();
        RegisterSpineGroupPreviews(workspaceId, bundleId, parts, previewHandler);
        previewHandler.RegisterPreview(CreateTextPreview(
            workspaceId,
            bundleId,
            parts.SkeletonAssetId,
            "json skeleton",
            "{\"skeleton\":{\"spine\":\"4.2\"},\"bones\":[]}"));
        var fileStore = new TestAssetExtractionFileStore();
        var changeStore = new TestWorkspaceChangeStore();
        var singleHandler = CreateHandler(registry, previewHandler, fileStore, changeStore);
        var handler = new ExtractSpineAssetGroupHandler(
            registry,
            new LoadSpineAssetGroupDetailsHandler(registry, previewHandler),
            singleHandler);

        var result = await handler.HandleAsync(new ExtractSpineAssetGroupCommand(
            workspaceId,
            WorkspaceKey,
            WorkspaceDataRootPath,
            bundleId,
            "spine-group:bundle.char_1001_amiya2:assets/characters/char_1001_amiya2",
            @"D:\Selected",
            UseContentAwareSkeletonFileExtension: true));

        TestAssert.True(result.IsSuccess, "Content-aware Spine group extraction should succeed.");
        TestAssert.True(
            fileStore.Requests.Any(static request => request.SuggestedOutputPath.EndsWith(
                "char_1001_amiya2.json",
                StringComparison.OrdinalIgnoreCase)),
            "A JSON skeleton should use a clean .json file name when the preference is enabled.");
        TestAssert.True(
            fileStore.Requests.All(static request => !request.SuggestedOutputPath.EndsWith(
                ".skel.json",
                StringComparison.OrdinalIgnoreCase)),
            "The content-aware name should not retain a stale .skel segment.");
    }

    public static async Task ExtractSpineAssetGroupHandler_ShouldUseExtractionPlanDetailsMode()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.char_1001_amiya2");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        var parts = RegisterSpineGroupAssets(workspaceId, bundleId, registry);
        var previewHandler = new TestAssetPreviewHandler();
        RegisterSpineGroupPreviews(workspaceId, bundleId, parts, previewHandler);
        var fileStore = new TestAssetExtractionFileStore();
        var changeStore = new TestWorkspaceChangeStore();
        var singleHandler = CreateHandler(registry, previewHandler, fileStore, changeStore);
        var detailsHandler = new RecordingSpineGroupDetailsHandler(CreateSpineGroupDto(bundleId));
        var recorder = new WorkspaceExtractionOperationRecorder(
            new TestWorkspaceDataStoreFactory(changeStore),
            new WorkspaceOperationLogService());
        var handler = new ExtractSpineAssetGroupHandler(registry, detailsHandler, singleHandler, recorder);

        var result = await handler.HandleAsync(new ExtractSpineAssetGroupCommand(
            workspaceId,
            WorkspaceKey,
            WorkspaceDataRootPath,
            bundleId,
            "spine-group:bundle.char_1001_amiya2:assets/characters/char_1001_amiya2",
            @"D:\Selected"));

        TestAssert.True(result.IsSuccess, "Spine group extraction should succeed with recorded details.");
        var query = TestAssert.Single(detailsHandler.Queries);
        TestAssert.Equal(SpineAssetGroupDetailsLoadMode.ExtractionPlan, query.LoadMode, "Spine group extraction should use the light extraction planning mode instead of rebuilding full preview details.");
    }

    public static async Task ExtractSpineAssetGroupHandler_ShouldPreferCatalogPlan_WhenAvailable()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.char_1001_amiya2");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        var parts = RegisterSpineGroupAssets(workspaceId, bundleId, registry);
        var previewHandler = new TestAssetPreviewHandler();
        RegisterSpineGroupPreviews(workspaceId, bundleId, parts, previewHandler);
        var fileStore = new TestAssetExtractionFileStore();
        var changeStore = new TestWorkspaceChangeStore();
        var singleHandler = CreateHandler(registry, previewHandler, fileStore, changeStore);
        var detailsHandler = new RecordingSpineGroupDetailsHandler(CreateSpineGroupDto(bundleId));
        var groupsHandler = new RecordingSpineGroupListHandler();
        var handler = new ExtractSpineAssetGroupHandler(
            registry,
            detailsHandler,
            singleHandler,
            loadSpineAssetGroupsHandler: groupsHandler);

        var result = await handler.HandleAsync(new ExtractSpineAssetGroupCommand(
            workspaceId,
            WorkspaceKey,
            WorkspaceDataRootPath,
            bundleId,
            "spine-group:bundle.char_1001_amiya2:assets/characters/char_1001_amiya2",
            @"D:\Selected"));

        TestAssert.True(result.IsSuccess, "Spine group extraction should succeed from the catalog plan.");
        TestAssert.Equal(0, detailsHandler.Queries.Count, "Catalog-backed Spine extraction should not load group details before extracting.");
        var query = TestAssert.Single(groupsHandler.Queries);
        TestAssert.Equal(WorkspaceDataRootPath, query.WorkspaceDataRootPath, "Catalog-backed Spine extraction should pass workspace data root for persistent catalog reuse.");
        TestAssert.Equal(4, fileStore.Requests.Count, "Catalog-backed Spine extraction should export all known group parts.");
    }

    private static SpineAssetGroupDto CreateSpineGroupDto(BundleId bundleId) =>
        new(
            "spine-group:bundle.char_1001_amiya2:assets/characters/char_1001_amiya2",
            bundleId,
            "char_1001_amiya2",
            "assets/characters/char_1001_amiya2",
            SkeletonAssets:
            [
                new SpineAssetGroupPartDto(
                    SpineAssetGroupPartRole.Skeleton,
                    bundleId,
                    new AssetId("asset.skel"),
                    $"{bundleId.Value}.ab",
                    "char_1001_amiya2.skel.bytes",
                    "Assets/Characters/char_1001_amiya2.skel.bytes",
                    AssetType.TextAsset,
                    "TextAsset")
            ],
            AtlasAssets:
            [
                new SpineAssetGroupPartDto(
                    SpineAssetGroupPartRole.Atlas,
                    bundleId,
                    new AssetId("asset.atlas"),
                    $"{bundleId.Value}.ab",
                    "char_1001_amiya2.atlas",
                    "Assets/Characters/char_1001_amiya2.atlas",
                    AssetType.TextAsset,
                    "TextAsset")
            ],
            TexturePageAssets:
            [
                new SpineAssetGroupPartDto(
                    SpineAssetGroupPartRole.TexturePage,
                    bundleId,
                    new AssetId("asset.texture"),
                    $"{bundleId.Value}.ab",
                    "char_1001_amiya2",
                    "Assets/Characters/char_1001_amiya2.png",
                    AssetType.Texture2D,
                    "Texture2D"),
                new SpineAssetGroupPartDto(
                    SpineAssetGroupPartRole.TexturePage,
                    bundleId,
                    new AssetId("asset.texture.alpha"),
                    $"{bundleId.Value}.ab",
                    "char_1001_amiya2_alpha",
                    "Assets/Characters/char_1001_amiya2_alpha.png",
                    AssetType.Texture2D,
                    "Texture2D")
            ],
            Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>());

    private sealed class RecordingSpineGroupListHandler :
        IQueryHandler<LoadSpineAssetGroupsQuery, Result<SpineAssetGroupListResultDto>>
    {
        public List<LoadSpineAssetGroupsQuery> Queries { get; } = [];

        public Task<Result<SpineAssetGroupListResultDto>> HandleAsync(
            LoadSpineAssetGroupsQuery query,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            var group = CreateSpineGroupDto(query.BundleId);
            return Task.FromResult(Result<SpineAssetGroupListResultDto>.Success(
                new SpineAssetGroupListResultDto(
                    query.WorkspaceId,
                    query.BundleId,
                    $"{query.BundleId.Value}.ab",
                    query.SearchKeyword,
                    TotalCount: 1,
                    FilteredCount: 1,
                    Items: [group])));
        }
    }

    private sealed class RecordingSpineGroupDetailsHandler :
        IQueryHandler<LoadSpineAssetGroupDetailsQuery, Result<SpineAssetGroupDetailsDto>>
    {
        private readonly SpineAssetGroupDto _group;

        public RecordingSpineGroupDetailsHandler(SpineAssetGroupDto group)
        {
            _group = group;
        }

        public List<LoadSpineAssetGroupDetailsQuery> Queries { get; } = [];

        public Task<Result<SpineAssetGroupDetailsDto>> HandleAsync(
            LoadSpineAssetGroupDetailsQuery query,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return Task.FromResult(Result<SpineAssetGroupDetailsDto>.Success(
                new SpineAssetGroupDetailsDto(
                    _group,
                    SkeletonSummaries: Array.Empty<SpineSkeletonSummaryDto>(),
                    AtlasPages: Array.Empty<SpineAtlasPageDto>(),
                    Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>())));
        }
    }
}
