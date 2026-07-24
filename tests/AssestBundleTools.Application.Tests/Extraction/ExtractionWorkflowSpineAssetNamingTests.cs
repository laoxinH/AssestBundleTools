using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Services;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Extraction;

internal static partial class ExtractionWorkflowHandlerTests
{
    public static async Task ExtractAssetHandler_ShouldUseSkelExtension_ForDetectedSkeletonByDefault()
    {
        var context = CreateExtensionlessSkeletonExtractionContext("default-skel");
        var handler = context.CreateHandler();

        var result = await handler.HandleAsync(new ExtractAssetCommand(
            context.WorkspaceId,
            context.BundleId,
            context.AssetId,
            WorkspaceKey,
            WorkspaceDataRootPath,
            @"D:\Exports\10010_skin_Alps06.txt",
            PreserveSuggestedFileName: true,
            SpineSkeletonExtensionMode: SpineSkeletonExportExtensionMode.AutoDetectUniformSkel));

        TestAssert.True(result.IsSuccess, "Detected skeleton extraction should succeed.");
        TestAssert.True(
            TestAssert.Single(context.FileStore.Requests).SuggestedOutputPath.EndsWith(
                "10010_skin_Alps06.skel",
                StringComparison.Ordinal),
            "Default skeleton naming should replace the generic TextAsset extension with .skel.");
    }

    public static async Task ExtractAssetHandler_ShouldUseJsonExtension_ForDetectedJsonSkeletonWhenEnabled()
    {
        var context = CreateExtensionlessSkeletonExtractionContext("content-aware-json");
        var handler = context.CreateHandler();

        var result = await handler.HandleAsync(new ExtractAssetCommand(
            context.WorkspaceId,
            context.BundleId,
            context.AssetId,
            WorkspaceKey,
            WorkspaceDataRootPath,
            @"D:\Exports\10010_skin_Alps06.txt",
            PreserveSuggestedFileName: true,
            SpineSkeletonExtensionMode: SpineSkeletonExportExtensionMode.AutoDetectContentAware));

        TestAssert.True(result.IsSuccess, "Content-aware skeleton extraction should succeed.");
        TestAssert.True(
            TestAssert.Single(context.FileStore.Requests).SuggestedOutputPath.EndsWith(
                "10010_skin_Alps06.json",
                StringComparison.Ordinal),
            "JSON skeleton naming should replace the generic TextAsset extension with .json.");
    }

    public static async Task ExtractAssetHandler_ShouldKeepTxtExtension_ForOrdinaryTextAssetDuringSkeletonDetection()
    {
        var workspaceId = new WorkspaceId("workspace-ordinary-text");
        var bundleId = new BundleId("bundle.text");
        var assetId = new AssetId("asset.readme");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.TextAsset,
            "readme",
            "Assets/Text/readme"));
        var previewHandler = new TestAssetPreviewHandler();
        previewHandler.RegisterPreview(CreateTextPreview(workspaceId, bundleId, assetId, "text", "plain text"));
        var fileStore = new TestAssetExtractionFileStore();
        var changeStore = new TestWorkspaceChangeStore();
        var handler = new ExtractAssetHandler(
            registry,
            new WorkspaceAssetExtractionExporter(previewHandler, fileStore),
            new WorkspaceExtractionOperationRecorder(
                new TestWorkspaceDataStoreFactory(changeStore),
                new WorkspaceOperationLogService()),
            new TestSpineAssetGroupsHandler(Array.Empty<SpineAssetGroupDto>()));

        var result = await handler.HandleAsync(new ExtractAssetCommand(
            workspaceId,
            bundleId,
            assetId,
            WorkspaceKey,
            WorkspaceDataRootPath,
            @"D:\Exports\readme.txt",
            PreserveSuggestedFileName: true,
            SpineSkeletonExtensionMode: SpineSkeletonExportExtensionMode.AutoDetectUniformSkel));

        TestAssert.True(result.IsSuccess, "Ordinary text extraction should succeed while skeleton detection is enabled.");
        TestAssert.True(
            TestAssert.Single(fileStore.Requests).SuggestedOutputPath.EndsWith("readme.txt", StringComparison.Ordinal),
            "An ordinary TextAsset must keep its .txt extension.");
    }

    private static SkeletonExtractionTestContext CreateExtensionlessSkeletonExtractionContext(string suffix)
    {
        var workspaceId = new WorkspaceId($"workspace-skeleton-{suffix}");
        var bundleId = new BundleId($"bundle.skeleton.{suffix}");
        var assetId = new AssetId($"asset.skeleton.{suffix}");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.TextAsset,
            "10010_skin_Alps06",
            "CAB-skeleton/10010_skin_Alps06"));
        var previewHandler = new TestAssetPreviewHandler();
        previewHandler.RegisterPreview(CreateTextPreview(
            workspaceId,
            bundleId,
            assetId,
            "skeleton",
            Encoding.UTF8.GetString("{\"skeleton\":{\"spine\":\"4.2.0\"}}"u8)));
        var fileStore = new TestAssetExtractionFileStore();
        var changeStore = new TestWorkspaceChangeStore();
        var skeletonPart = new SpineAssetGroupPartDto(
            SpineAssetGroupPartRole.Skeleton,
            bundleId,
            assetId,
            "Skeleton Bundle",
            "10010_skin_Alps06",
            "CAB-skeleton/10010_skin_Alps06",
            AssetType.TextAsset,
            nameof(AssetType.TextAsset));
        var group = new SpineAssetGroupDto(
            $"group-{suffix}",
            bundleId,
            "10010_skin_Alps06",
            "10010_skin_alps06",
            [skeletonPart],
            Array.Empty<SpineAssetGroupPartDto>(),
            Array.Empty<SpineAssetGroupPartDto>(),
            Array.Empty<SpineAssetGroupDiagnosticDto>());
        return new SkeletonExtractionTestContext(
            workspaceId,
            bundleId,
            assetId,
            registry,
            previewHandler,
            fileStore,
            changeStore,
            new TestSpineAssetGroupsHandler([group]));
    }

    private sealed record SkeletonExtractionTestContext(
        WorkspaceId WorkspaceId,
        BundleId BundleId,
        AssetId AssetId,
        TestWorkspaceSessionRegistry Registry,
        TestAssetPreviewHandler PreviewHandler,
        TestAssetExtractionFileStore FileStore,
        TestWorkspaceChangeStore ChangeStore,
        TestSpineAssetGroupsHandler SpineGroupsHandler)
    {
        public ExtractAssetHandler CreateHandler() =>
            new(
                Registry,
                new WorkspaceAssetExtractionExporter(PreviewHandler, FileStore),
                new WorkspaceExtractionOperationRecorder(
                    new TestWorkspaceDataStoreFactory(ChangeStore),
                    new WorkspaceOperationLogService()),
                SpineGroupsHandler);
    }

    private sealed class TestSpineAssetGroupsHandler(
        IReadOnlyList<SpineAssetGroupDto> groups)
        : IQueryHandler<LoadSpineAssetGroupsQuery, Result<SpineAssetGroupListResultDto>>
    {
        public Task<Result<SpineAssetGroupListResultDto>> HandleAsync(
            LoadSpineAssetGroupsQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<SpineAssetGroupListResultDto>.Success(
                new SpineAssetGroupListResultDto(
                    query.WorkspaceId,
                    query.BundleId,
                    query.BundleId.Value,
                    query.SearchKeyword,
                    groups.Count,
                    groups.Count,
                    groups)));
    }
}
