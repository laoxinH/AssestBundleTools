using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Queries;
using AssestBundleTools.Application.Extraction.Services;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Extraction;

internal static partial class ExtractionWorkflowHandlerTests
{
    public static async Task ExtractAssetHandler_ShouldWriteFullTextPayload_AndRecordOperation()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.dialogue");
        var assetId = new AssetId("asset.dialogue");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.TextAsset,
            "Dialogue",
            "Assets/Dialogues/dialogue.json"));
        var previewHandler = new TestAssetPreviewHandler();
        previewHandler.RegisterPreview(CreateTextPreview(workspaceId, bundleId, assetId, "preview", "full\npayload"));
        var fileStore = new TestAssetExtractionFileStore();
        var changeStore = new TestWorkspaceChangeStore();
        var handler = CreateHandler(registry, previewHandler, fileStore, changeStore);

        var result = await handler.HandleAsync(new ExtractAssetCommand(
            workspaceId,
            bundleId,
            assetId,
            WorkspaceKey,
            WorkspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Text extraction should succeed.");
        TestAssert.True(registry.ExtractedAssets.Contains((bundleId.Value, assetId.Value)), "Successful export should mark the asset extracted.");
        var writeRequest = TestAssert.Single(fileStore.Requests);
        TestAssert.True(writeRequest.SuggestedOutputPath.EndsWith("dialogue.json", StringComparison.Ordinal), "Text extraction should preserve the source text extension.");
        TestAssert.Equal("full\npayload", Encoding.UTF8.GetString(writeRequest.Content ?? []), "Extraction should write the full text payload, not the bounded display text.");
        TestAssert.Equal(WorkspaceKey, previewHandler.LastQuery?.WorkspaceKey, "Preview query should use the workspace key from the command.");
        TestAssert.Equal(WorkspaceDataRootPath, previewHandler.LastQuery?.WorkspaceDataRootPath, "Preview query should use the workspace data root from the command.");

        var operation = TestAssert.Single(changeStore.SavedChangeSet?.Operations ?? Array.Empty<WorkspaceOperationRecordDto>());
        TestAssert.Equal(WorkspaceOperationKind.ExtractAsset, operation.OperationKind, "Extraction should append an asset operation.");
        TestAssert.Equal(WorkspaceOperationStatus.Applied, operation.Status, "Successful extraction should be applied.");
        TestAssert.Equal(fileStore.LastResult?.OutputPath, operation.Result?.OutputPath, "Operation result should keep the real output path.");
        TestAssert.Equal(true, operation.Result?.OutputFileExists, "Operation result should record output file availability.");
    }

    public static async Task ExtractAssetHandler_ShouldPreserveReadableSkelTextPayload()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.spine");
        var assetId = new AssetId("asset.skel");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.TextAsset,
            "Hero",
            "Assets/Hero/Hero.skel"));
        var fullSkeletonJson = """
            {
              "skeleton": { "spine": "4.2.74" },
              "bones": [{ "name": "root" }]
            }
            """;
        var previewHandler = new TestAssetPreviewHandler();
        previewHandler.RegisterPreview(CreateTextPreview(workspaceId, bundleId, assetId, "{...}", fullSkeletonJson));
        var fileStore = new TestAssetExtractionFileStore();
        var handler = CreateHandler(registry, previewHandler, fileStore, new TestWorkspaceChangeStore());

        var result = await handler.HandleAsync(new ExtractAssetCommand(
            workspaceId,
            bundleId,
            assetId,
            WorkspaceKey,
            WorkspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Readable .skel text extraction should succeed.");
        var writeRequest = TestAssert.Single(fileStore.Requests);
        TestAssert.True(writeRequest.SuggestedOutputPath.EndsWith("Hero.skel", StringComparison.Ordinal), "Readable .skel extraction should preserve the skeleton extension.");
        TestAssert.Equal(fullSkeletonJson, Encoding.UTF8.GetString(writeRequest.Content ?? []), "Readable .skel extraction should write the full text payload.");
        TestAssert.Equal(AssetPreviewKind.Text, result.Value?.OutputKind, "Readable .skel extraction should keep the text output kind.");
    }

    public static async Task ExtractAssetHandler_ShouldWriteImagePayload_WithPngOutput()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.textures");
        var assetId = new AssetId("asset.texture");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.Texture2D,
            "HeroTexture",
            "Assets/Textures/HeroTexture"));
        var previewHandler = new TestAssetPreviewHandler();
        previewHandler.RegisterPreview(CreateBinaryPreview(
            workspaceId,
            bundleId,
            assetId,
            AssetPreviewKind.Image,
            "image/png",
            [0x89, 0x50, 0x4E, 0x47]));
        var fileStore = new TestAssetExtractionFileStore();
        var handler = CreateHandler(registry, previewHandler, fileStore, new TestWorkspaceChangeStore());

        var result = await handler.HandleAsync(new ExtractAssetCommand(
            workspaceId,
            bundleId,
            assetId,
            WorkspaceKey,
            WorkspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Image extraction should succeed.");
        var writeRequest = TestAssert.Single(fileStore.Requests);
        TestAssert.True(writeRequest.SuggestedOutputPath.EndsWith("HeroTexture.png", StringComparison.Ordinal), "Image extraction should write PNG output.");
        TestAssert.Equal(4, writeRequest.Content?.Length ?? 0, "Image extraction should write the binary preview payload.");
        TestAssert.Equal(AssetPreviewKind.Image, result.Value?.OutputKind, "Result should expose the output kind.");
    }

    public static async Task ExtractAssetHandler_ShouldPreserveUserSelectedOutputFileName()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.textures");
        var assetId = new AssetId("asset.texture");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.Texture2D,
            "HeroTexture",
            "Assets/Textures/HeroTexture"));
        var previewHandler = new TestAssetPreviewHandler();
        previewHandler.RegisterPreview(CreateBinaryPreview(
            workspaceId,
            bundleId,
            assetId,
            AssetPreviewKind.Image,
            "image/png",
            [0x89, 0x50, 0x4E, 0x47]));
        var fileStore = new TestAssetExtractionFileStore();
        var handler = CreateHandler(registry, previewHandler, fileStore, new TestWorkspaceChangeStore());
        const string selectedOutputPath = @"D:\Selected\amiya-export.custom";

        var result = await handler.HandleAsync(new ExtractAssetCommand(
            workspaceId,
            bundleId,
            assetId,
            WorkspaceKey,
            WorkspaceDataRootPath,
            selectedOutputPath,
            PreserveSuggestedFileName: true));

        TestAssert.True(result.IsSuccess, "Image extraction with a user-selected path should succeed.");
        var writeRequest = TestAssert.Single(fileStore.Requests);
        TestAssert.Equal(selectedOutputPath, writeRequest.SuggestedOutputPath, "The extractor should preserve the explicit file name selected by the user.");
    }

    public static async Task ExtractWorkspaceAssetsHandler_ShouldExtractBatch_AndAppendBatchOperation()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var textBundleId = new BundleId("bundle.dialogue");
        var imageBundleId = new BundleId("bundle.textures");
        var textAssetId = new AssetId("asset.dialogue");
        var imageAssetId = new AssetId("asset.texture");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            textBundleId,
            textAssetId,
            AssetType.TextAsset,
            "Dialogue",
            "Assets/Dialogues/dialogue.txt"));
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            imageBundleId,
            imageAssetId,
            AssetType.Texture2D,
            "HeroTexture",
            "Assets/Textures/HeroTexture"));
        var previewHandler = new TestAssetPreviewHandler();
        previewHandler.RegisterPreview(CreateTextPreview(workspaceId, textBundleId, textAssetId, "dialogue", "dialogue"));
        previewHandler.RegisterPreview(CreateBinaryPreview(
            workspaceId,
            imageBundleId,
            imageAssetId,
            AssetPreviewKind.Image,
            "image/png",
            [1, 2, 3]));
        var fileStore = new TestAssetExtractionFileStore();
        var changeStore = new TestWorkspaceChangeStore();
        var singleHandler = CreateHandler(registry, previewHandler, fileStore, changeStore);
        var recorder = new WorkspaceExtractionOperationRecorder(
            new TestWorkspaceDataStoreFactory(changeStore),
            new WorkspaceOperationLogService());
        var batchHandler = new ExtractWorkspaceAssetsHandler(registry, singleHandler, recorder);

        var result = await batchHandler.HandleAsync(new ExtractWorkspaceAssetsCommand(
            workspaceId,
            WorkspaceKey: WorkspaceKey,
            WorkspaceDataRootPath: WorkspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Batch extraction should succeed.");
        TestAssert.Equal(2, result.Value?.ExtractedAssetCount ?? 0, "Batch extraction should report extracted assets.");
        TestAssert.Equal(0, result.Value?.FailedAssetCount ?? -1, "Batch extraction should not report failures.");
        TestAssert.Equal(2, fileStore.Requests.Count, "Batch extraction should write every planned asset.");

        var operations = changeStore.SavedChangeSet?.Operations ?? Array.Empty<WorkspaceOperationRecordDto>();
        TestAssert.Equal(3, operations.Count, "Batch extraction should keep two asset operations plus one batch operation.");
        TestAssert.Equal(WorkspaceOperationKind.ExtractBatch, operations[^1].OperationKind, "Batch operation should be appended last.");
        TestAssert.Equal(2, operations[^1].Result?.CompletedCount ?? 0, "Batch operation should record completed count.");
    }

    public static async Task ExtractWorkspaceAssetsHandler_ShouldWriteExportManifest_ForBatchReplacementReadback()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.manifest");
        var assetId = new AssetId("asset.texture");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.Texture2D,
            "HeroTexture",
            "Assets/Textures/HeroTexture",
            pathId: 42));
        var previewHandler = new TestAssetPreviewHandler();
        previewHandler.RegisterPreview(CreateBinaryPreview(
            workspaceId,
            bundleId,
            assetId,
            AssetPreviewKind.Image,
            "image/png",
            [1, 2, 3]));
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
            exportManifestStore: manifestStore);

        var result = await batchHandler.HandleAsync(new ExtractWorkspaceAssetsCommand(
            workspaceId,
            OutputDirectory: @"D:\Out",
            WorkspaceKey: WorkspaceKey,
            WorkspaceDataRootPath: WorkspaceDataRootPath,
            SelectedBundleIds: new[] { bundleId },
            SelectedCategories: new[] { WorkspaceBatchExtractionCategoryDto.FromAssetType(AssetType.Texture2D) }));

        TestAssert.True(result.IsSuccess, "Batch extraction should succeed and write a manifest.");
        TestAssert.Equal(@"D:\Out\_abt-export-manifest.json", result.Value?.ExportManifestPath, "Result should expose the manifest path.");
        TestAssert.Equal(1, result.Value?.ExportManifestItemCount ?? 0, "Result should expose manifest item count.");

        TestAssert.True(manifestStore.LastRequest is not null, "Manifest store should receive a save request.");
        var manifestRequest = manifestStore.LastRequest!;
        TestAssert.Equal(WorkspaceKey, manifestRequest.WorkspaceKey, "Manifest should keep the workspace key.");
        TestAssert.Equal(@"D:\Out", manifestRequest.OutputRoot, "Manifest should keep the batch output root.");
        var manifestItem = TestAssert.Single(manifestRequest.Items);
        TestAssert.Equal(bundleId, manifestItem.BundleId, "Manifest source item should keep bundle identity.");
        TestAssert.Equal(assetId, manifestItem.AssetId, "Manifest source item should keep asset identity.");
        TestAssert.Equal(42L, manifestItem.PathId, "Manifest source item should keep path id for replacement diagnostics.");
        TestAssert.Equal("Texture2D", manifestItem.AssetType, "Manifest source item should keep asset type.");
        TestAssert.Equal("HeroTexture", manifestItem.OriginalName, "Manifest source item should keep original name.");
        TestAssert.Equal("HeroTexture", manifestItem.DisambiguatedName, "Manifest source item should keep projected display/export name.");
        TestAssert.Equal("Assets/Textures/HeroTexture", manifestItem.ContainerPath, "Manifest source item should keep original container path.");
        TestAssert.Equal(WorkspaceExportManifestOutputRole.Asset, manifestItem.OutputRole, "Ordinary asset exports should be marked as Asset.");

        var batchOperation = changeStore.SavedChangeSet?.Operations?.LastOrDefault();
        TestAssert.Equal(WorkspaceOperationKind.ExtractBatch, batchOperation?.OperationKind, "Batch operation should still be appended.");
        TestAssert.Equal(@"D:\Out\_abt-export-manifest.json", batchOperation?.Result?.OutputPath, "Batch operation should record manifest path.");
        TestAssert.Equal(true, batchOperation?.Result?.OutputFileExists, "Batch operation should mark manifest output availability.");
    }

    public static async Task ExtractWorkspaceAssetsHandler_ShouldReportWorkspaceBatchProgress()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.dynamic");
        var shaderAssetId = new AssetId("asset.shader");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            shaderAssetId,
            AssetType.Unknown,
            "HeroShader",
            "Assets/HeroShader.shader",
            "Shader"));
        var progress = new RecordingProgress<WorkspaceBatchExtractionProgressDto>();
        var batchHandler = new ExtractWorkspaceAssetsHandler(registry);

        var result = await batchHandler.HandleAsync(new ExtractWorkspaceAssetsCommand(
            workspaceId,
            WorkspaceKey: WorkspaceKey,
            WorkspaceDataRootPath: WorkspaceDataRootPath,
            SelectedBundleIds: new[] { bundleId },
            SelectedCategories: new[] { WorkspaceBatchExtractionCategoryDto.FromAssetType(AssetType.Unknown, "Shader") },
            Progress: progress));

        TestAssert.True(result.IsSuccess, "Batch extraction should succeed.");
        TestAssert.True(
            progress.Reports.Any(static report =>
                report.Kind == WorkspaceBatchExtractionProgressKind.Preparing &&
                report.TotalItemCount == 1 &&
                report.CandidateBundleCount == 1),
            "Progress should report a preparing snapshot with deterministic totals.");
        TestAssert.True(
            progress.Reports.Any(report =>
                report.Kind == WorkspaceBatchExtractionProgressKind.Asset &&
                report.BundleDisplayName == "bundle.dynamic.ab" &&
                report.ItemDisplayName == "HeroShader" &&
                report.AssetType == AssetType.Unknown &&
                report.AssetTypeDisplayName == "Shader"),
            "Progress should identify the current bundle, item, and workspace-provided asset type name.");
        TestAssert.True(
            progress.Reports.Any(static report =>
                report.Kind == WorkspaceBatchExtractionProgressKind.Asset &&
                report.CompletedItemCount == 1 &&
                report.SucceededItemCount == 1 &&
                report.FailedItemCount == 0),
            "Progress should publish the completed item count after extraction.");
    }

    public static async Task GetWorkspaceBatchExtractionPreviewHandler_ShouldRespectSelectedBundlesAndCategories()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleA = new BundleId("bundle.a");
        var bundleB = new BundleId("bundle.b");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleA,
            new AssetId("asset.texture.a"),
            AssetType.Texture2D,
            "TextureA",
            "Assets/TextureA"));
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleA,
            new AssetId("asset.text.a"),
            AssetType.TextAsset,
            "TextA",
            "Assets/TextA.txt"));
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleB,
            new AssetId("asset.sprite.b"),
            AssetType.Sprite,
            "SpriteB",
            "Assets/SpriteB"));
        var handler = new GetWorkspaceBatchExtractionPreviewHandler(registry);

        var result = await handler.HandleAsync(new GetWorkspaceBatchExtractionPreviewQuery(
            workspaceId,
            OutputDirectory: @"D:\Out",
            SelectedBundleIds: new[] { bundleA },
            SelectedCategories: new[]
            {
                WorkspaceBatchExtractionCategoryDto.FromAssetType(AssetType.Texture2D),
                WorkspaceBatchExtractionCategoryDto.FromAssetType(AssetType.Sprite)
            }));

        TestAssert.True(result.IsSuccess, "Workspace batch preview should succeed.");
        TestAssert.Equal(1, result.Value?.CandidateAssetCount ?? 0, "Preview should only include matching assets from selected bundles.");
        TestAssert.Equal(1, result.Value?.CandidateBundleCount ?? 0, "Preview should only include bundles with matching candidates.");
        var bundlePlan = TestAssert.Single(result.Value?.BundlePlans ?? Array.Empty<WorkspaceBatchExtractionBundlePlanDto>());
        TestAssert.Equal(bundleA, bundlePlan.BundleId, "Preview should keep the selected bundle identity.");
        TestAssert.Equal(1, bundlePlan.AssetCount, "Preview should aggregate only matching category assets.");
        TestAssert.Equal(2, result.Value?.SelectedCategories?.Count ?? 0, "Preview should echo the explicit category selection.");
        var categoryPlans = result.Value?.CategoryPlans ?? Array.Empty<WorkspaceBatchExtractionCategoryPlanDto>();
        TestAssert.True(
            categoryPlans.Any(plan =>
                plan.Category == WorkspaceBatchExtractionCategoryDto.FromAssetType(AssetType.Texture2D) &&
                plan.CandidateCount == 1),
            "Preview should expose per-category candidate counts for the selected bundle range.");
        TestAssert.True(
            categoryPlans.Any(plan =>
                plan.Category == WorkspaceBatchExtractionCategoryDto.FromAssetType(AssetType.TextAsset) &&
                plan.CandidateCount == 1),
            "Category counts should include unselected types that exist in the selected bundles.");
        TestAssert.False(
            categoryPlans.Any(plan => plan.Category == WorkspaceBatchExtractionCategoryDto.FromAssetType(AssetType.Sprite)),
            "Category counts should not include asset types that only exist in unchecked bundles.");
    }

    public static async Task GetWorkspaceBatchExtractionPreviewHandler_ShouldExposeUnknownTypeDisplayNames()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.dynamic");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            new AssetId("asset.shader"),
            AssetType.Unknown,
            "HeroShader",
            "Assets/HeroShader.shader",
            "Shader"));
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            new AssetId("asset.behaviour"),
            AssetType.Unknown,
            "HeroBehaviour",
            "Assets/HeroBehaviour.asset",
            "MonoBehaviour"));
        var handler = new GetWorkspaceBatchExtractionPreviewHandler(registry);

        var result = await handler.HandleAsync(new GetWorkspaceBatchExtractionPreviewQuery(
            workspaceId,
            OutputDirectory: @"D:\Out",
            SelectedBundleIds: new[] { bundleId },
            SelectedCategories: new[] { WorkspaceBatchExtractionCategoryDto.All }));

        TestAssert.True(result.IsSuccess, "Workspace batch preview should succeed for dynamic unknown Unity types.");
        var categoryPlans = result.Value?.CategoryPlans ?? Array.Empty<WorkspaceBatchExtractionCategoryPlanDto>();
        TestAssert.True(
            categoryPlans.Any(static plan =>
                plan.Category == WorkspaceBatchExtractionCategoryDto.FromAssetType(AssetType.Unknown, "Shader") &&
                plan.CandidateCount == 1),
            "Category plans should preserve the workspace-provided Shader type name.");
        TestAssert.True(
            categoryPlans.Any(static plan =>
                plan.Category == WorkspaceBatchExtractionCategoryDto.FromAssetType(AssetType.Unknown, "MonoBehaviour") &&
                plan.CandidateCount == 1),
            "Category plans should not collapse different unknown Unity type names into one generic Unknown row.");
    }

    public static async Task ExtractWorkspaceAssetsHandler_ShouldRespectSelectedBundlesAndCategories()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleA = new BundleId("bundle.a");
        var bundleB = new BundleId("bundle.b");
        var textureA = new AssetId("asset.texture.a");
        var textA = new AssetId("asset.text.a");
        var textureB = new AssetId("asset.texture.b");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleA,
            textureA,
            AssetType.Texture2D,
            "TextureA",
            "Assets/TextureA"));
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleA,
            textA,
            AssetType.TextAsset,
            "TextA",
            "Assets/TextA.txt"));
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleB,
            textureB,
            AssetType.Texture2D,
            "TextureB",
            "Assets/TextureB"));
        var previewHandler = new TestAssetPreviewHandler();
        previewHandler.RegisterPreview(CreateBinaryPreview(
            workspaceId,
            bundleA,
            textureA,
            AssetPreviewKind.Image,
            "image/png",
            [1, 2, 3]));
        previewHandler.RegisterPreview(CreateTextPreview(workspaceId, bundleA, textA, "text", "text"));
        previewHandler.RegisterPreview(CreateBinaryPreview(
            workspaceId,
            bundleB,
            textureB,
            AssetPreviewKind.Image,
            "image/png",
            [4, 5, 6]));
        var fileStore = new TestAssetExtractionFileStore();
        var changeStore = new TestWorkspaceChangeStore();
        var singleHandler = CreateHandler(registry, previewHandler, fileStore, changeStore);
        var recorder = new WorkspaceExtractionOperationRecorder(
            new TestWorkspaceDataStoreFactory(changeStore),
            new WorkspaceOperationLogService());
        var batchHandler = new ExtractWorkspaceAssetsHandler(registry, singleHandler, recorder);

        var result = await batchHandler.HandleAsync(new ExtractWorkspaceAssetsCommand(
            workspaceId,
            OutputDirectory: @"D:\Out",
            WorkspaceKey: WorkspaceKey,
            WorkspaceDataRootPath: WorkspaceDataRootPath,
            SelectedBundleIds: new[] { bundleA },
            SelectedCategories: new[] { WorkspaceBatchExtractionCategoryDto.FromAssetType(AssetType.Texture2D) }));

        TestAssert.True(result.IsSuccess, "Selected bundle/category extraction should succeed.");
        TestAssert.Equal(1, result.Value?.ExtractedAssetCount ?? 0, "Only the selected bundle and category should be extracted.");
        var writeRequest = TestAssert.Single(fileStore.Requests);
        TestAssert.True(
            writeRequest.SuggestedOutputPath.EndsWith(@"bundle.a.ab\Texture2D\TextureA.png", StringComparison.OrdinalIgnoreCase),
            "Workspace batch output should be grouped by bundle display name and asset type.");
        TestAssert.Equal(1, result.Value?.SelectedCategories?.Count ?? 0, "Result should echo the explicit category selection.");
    }

    public static async Task ExtractWorkspaceAssetsHandler_ShouldFilterUnknownTypeByDisplayName()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.dynamic");
        var shaderAssetId = new AssetId("asset.shader");
        var behaviourAssetId = new AssetId("asset.behaviour");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            shaderAssetId,
            AssetType.Unknown,
            "HeroShader",
            "Assets/HeroShader.shader",
            "Shader"));
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            behaviourAssetId,
            AssetType.Unknown,
            "HeroBehaviour",
            "Assets/HeroBehaviour.asset",
            "MonoBehaviour"));
        var batchHandler = new ExtractWorkspaceAssetsHandler(registry);

        var result = await batchHandler.HandleAsync(new ExtractWorkspaceAssetsCommand(
            workspaceId,
            OutputDirectory: @"D:\Out",
            WorkspaceKey: WorkspaceKey,
            WorkspaceDataRootPath: WorkspaceDataRootPath,
            SelectedBundleIds: new[] { bundleId },
            SelectedCategories: new[] { WorkspaceBatchExtractionCategoryDto.FromAssetType(AssetType.Unknown, "Shader") }));

        TestAssert.True(result.IsSuccess, "Selected dynamic unknown type extraction should succeed.");
        TestAssert.Equal(1, result.Value?.ExtractedAssetCount ?? 0, "Only the matching unknown display-name category should be extracted.");
        TestAssert.True(registry.ExtractedAssets.Contains((bundleId.Value, shaderAssetId.Value)), "The Shader asset should be extracted.");
        TestAssert.False(registry.ExtractedAssets.Contains((bundleId.Value, behaviourAssetId.Value)), "The MonoBehaviour asset should not be extracted by the Shader category.");
        TestAssert.Equal(
            WorkspaceBatchExtractionCategoryDto.FromAssetType(AssetType.Unknown, "Shader"),
            TestAssert.Single(result.Value?.SelectedCategories ?? Array.Empty<WorkspaceBatchExtractionCategoryDto>()),
            "Result should preserve the selected unknown display-name category.");
    }

    public static async Task LoadAssetListHandler_ShouldDisambiguateDuplicateAssetNames()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.duplicates");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            new AssetId("asset.path-2"),
            AssetType.Texture2D,
            "shared_texture",
            "Assets/B/shared_texture.png",
            pathId: 2));
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            new AssetId("asset.path-1"),
            AssetType.Texture2D,
            "shared_texture",
            "Assets/A/shared_texture.png",
            pathId: 1));
        var handler = new LoadAssetListHandler(registry);

        var result = await handler.HandleAsync(new LoadAssetListQuery(workspaceId, bundleId));

        TestAssert.True(result.IsSuccess, "Asset list should load duplicate names.");
        var items = result.Value?.Items
            .OrderBy(static item => item.PathId)
            .ToArray() ?? Array.Empty<AssetListItemDto>();
        TestAssert.Equal(2, items.Length, "Both duplicate assets should remain visible.");
        TestAssert.Equal("shared_texture [1]", items[0].DisplayName, "First duplicate should receive a stable display suffix.");
        TestAssert.Equal("shared_texture [2]", items[1].DisplayName, "Second duplicate should receive a stable display suffix.");
        TestAssert.Equal("shared_texture [1].png", items[0].ExportFileName, "First duplicate should receive a unique export file name.");
        TestAssert.Equal("shared_texture [2].png", items[1].ExportFileName, "Second duplicate should receive a unique export file name.");
    }

    public static async Task ExtractWorkspaceAssetsHandler_ShouldDisambiguateDuplicateAssetOutputPaths()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.duplicates");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        var firstAssetId = new AssetId("asset.path-1");
        var secondAssetId = new AssetId("asset.path-2");
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            firstAssetId,
            AssetType.Texture2D,
            "shared_texture",
            "Assets/A/shared_texture.png",
            pathId: 1));
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            secondAssetId,
            AssetType.Texture2D,
            "shared_texture",
            "Assets/B/shared_texture.png",
            pathId: 2));
        var previewHandler = new TestAssetPreviewHandler();
        previewHandler.RegisterPreview(CreateBinaryPreview(
            workspaceId,
            bundleId,
            firstAssetId,
            AssetPreviewKind.Image,
            "image/png",
            [1]));
        previewHandler.RegisterPreview(CreateBinaryPreview(
            workspaceId,
            bundleId,
            secondAssetId,
            AssetPreviewKind.Image,
            "image/png",
            [2]));
        var fileStore = new TestAssetExtractionFileStore();
        var changeStore = new TestWorkspaceChangeStore();
        var singleHandler = CreateHandler(registry, previewHandler, fileStore, changeStore);
        var batchHandler = new ExtractWorkspaceAssetsHandler(
            registry,
            singleHandler,
            new WorkspaceExtractionOperationRecorder(
                new TestWorkspaceDataStoreFactory(changeStore),
                new WorkspaceOperationLogService()));

        var result = await batchHandler.HandleAsync(new ExtractWorkspaceAssetsCommand(
            workspaceId,
            OutputDirectory: @"D:\Out",
            WorkspaceKey: WorkspaceKey,
            WorkspaceDataRootPath: WorkspaceDataRootPath,
            SelectedBundleIds: new[] { bundleId },
            SelectedCategories: new[] { WorkspaceBatchExtractionCategoryDto.FromAssetType(AssetType.Texture2D) }));

        TestAssert.True(result.IsSuccess, "Batch extraction should succeed for duplicate asset names.");
        var requestedPaths = fileStore.Requests
            .Select(static request => request.SuggestedOutputPath)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        TestAssert.Equal(2, requestedPaths.Length, "Both duplicate assets should be written.");
        TestAssert.True(
            requestedPaths.Any(static path => path.EndsWith(@"bundle.duplicates.ab\Texture2D\shared_texture [1].png", StringComparison.OrdinalIgnoreCase)),
            "First duplicate output path should use [1] suffix.");
        TestAssert.True(
            requestedPaths.Any(static path => path.EndsWith(@"bundle.duplicates.ab\Texture2D\shared_texture [2].png", StringComparison.OrdinalIgnoreCase)),
            "Second duplicate output path should use [2] suffix.");
    }

    public static async Task ExtractWorkspaceAssetsHandler_ShouldExtractBundlesInParallel_AndKeepSameBundleSequential()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleA = new BundleId("bundle.a");
        var bundleB = new BundleId("bundle.b");
        var bundleC = new BundleId("bundle.c");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        var assets = new[]
        {
            (BundleId: bundleA, AssetId: new AssetId("asset.a.1"), Name: "A1"),
            (BundleId: bundleA, AssetId: new AssetId("asset.a.2"), Name: "A2"),
            (BundleId: bundleB, AssetId: new AssetId("asset.b.1"), Name: "B1"),
            (BundleId: bundleC, AssetId: new AssetId("asset.c.1"), Name: "C1")
        };
        foreach (var asset in assets)
        {
            registry.RegisterAsset(CreateAssetDetails(
                workspaceId,
                asset.BundleId,
                asset.AssetId,
                AssetType.TextAsset,
                asset.Name,
                $"Assets/{asset.Name}.txt"));
        }

        var activePreviews = 0;
        var maxConcurrentPreviews = 0;
        var activeBundleAPreviews = 0;
        var maxConcurrentBundleAPreviews = 0;
        var previewHandler = new TestAssetPreviewHandler
        {
            BeforeReturnAsync = async (query, cancellationToken) =>
            {
                var currentPreviews = Interlocked.Increment(ref activePreviews);
                RecordMaximumConcurrency(ref maxConcurrentPreviews, currentPreviews);
                var isBundleA = query.BundleId.Equals(bundleA);
                if (isBundleA)
                {
                    var currentBundleAPreviews = Interlocked.Increment(ref activeBundleAPreviews);
                    RecordMaximumConcurrency(ref maxConcurrentBundleAPreviews, currentBundleAPreviews);
                }

                try
                {
                    await Task.Delay(120, cancellationToken);
                }
                finally
                {
                    if (isBundleA)
                    {
                        Interlocked.Decrement(ref activeBundleAPreviews);
                    }

                    Interlocked.Decrement(ref activePreviews);
                }
            }
        };
        foreach (var asset in assets)
        {
            previewHandler.RegisterPreview(CreateTextPreview(workspaceId, asset.BundleId, asset.AssetId, asset.Name, asset.Name));
        }

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
            processorCountProvider: () => 8,
            memoryPressureEvaluator: () => false);

        var result = await batchHandler.HandleAsync(new ExtractWorkspaceAssetsCommand(
            workspaceId,
            WorkspaceKey: WorkspaceKey,
            WorkspaceDataRootPath: WorkspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Batch extraction should succeed.");
        TestAssert.Equal(4, result.Value?.ExtractedAssetCount ?? 0, "All planned assets should be extracted.");
        TestAssert.True(maxConcurrentPreviews > 1, "Different bundles should be extracted in parallel when CPU allows it.");
        TestAssert.Equal(1, maxConcurrentBundleAPreviews, "Assets from the same bundle should remain sequential.");
        TestAssert.Equal(5, changeStore.SavedChangeSet?.Operations?.Count ?? 0, "Parallel extraction should not lose operation records.");
    }

    public static async Task ExtractWorkspaceAssetsHandler_ShouldThrottleCrossBundleParallelism_WhenMemoryPressureIsHigh()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        var assets = Enumerable.Range(1, 4)
            .Select(index => (
                BundleId: new BundleId($"bundle.{index}"),
                AssetId: new AssetId($"asset.{index}"),
                Name: $"Asset{index}"))
            .ToArray();
        foreach (var asset in assets)
        {
            registry.RegisterAsset(CreateAssetDetails(
                workspaceId,
                asset.BundleId,
                asset.AssetId,
                AssetType.TextAsset,
                asset.Name,
                $"Assets/{asset.Name}.txt"));
        }

        var activePreviews = 0;
        var maxConcurrentPreviews = 0;
        var previewHandler = new TestAssetPreviewHandler
        {
            BeforeReturnAsync = async (_, cancellationToken) =>
            {
                var currentPreviews = Interlocked.Increment(ref activePreviews);
                RecordMaximumConcurrency(ref maxConcurrentPreviews, currentPreviews);
                try
                {
                    await Task.Delay(80, cancellationToken);
                }
                finally
                {
                    Interlocked.Decrement(ref activePreviews);
                }
            }
        };
        foreach (var asset in assets)
        {
            previewHandler.RegisterPreview(CreateTextPreview(workspaceId, asset.BundleId, asset.AssetId, asset.Name, asset.Name));
        }

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
            processorCountProvider: () => 8,
            memoryPressureEvaluator: () => true);

        var result = await batchHandler.HandleAsync(new ExtractWorkspaceAssetsCommand(
            workspaceId,
            WorkspaceKey: WorkspaceKey,
            WorkspaceDataRootPath: WorkspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Batch extraction should still succeed under memory pressure.");
        TestAssert.Equal(4, result.Value?.ExtractedAssetCount ?? 0, "All planned assets should be extracted.");
        TestAssert.Equal(1, maxConcurrentPreviews, "High memory pressure should throttle cross-bundle extraction to one active bundle.");
    }

    private static SpineGroupAssetIds RegisterSpineGroupAssets(
        WorkspaceId workspaceId,
        BundleId bundleId,
        TestWorkspaceSessionRegistry registry)
    {
        var parts = new SpineGroupAssetIds(
            SkeletonAssetId: new AssetId("asset.skel"),
            AtlasAssetId: new AssetId("asset.atlas"),
            TextureAssetId: new AssetId("asset.texture"),
            AlphaTextureAssetId: new AssetId("asset.texture.alpha"));
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            parts.SkeletonAssetId,
            AssetType.TextAsset,
            "char_1001_amiya2.skel.bytes",
            "Assets/Characters/char_1001_amiya2.skel.bytes"));
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            parts.AtlasAssetId,
            AssetType.TextAsset,
            "char_1001_amiya2.atlas",
            "Assets/Characters/char_1001_amiya2.atlas"));
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            parts.TextureAssetId,
            AssetType.Texture2D,
            "char_1001_amiya2",
            "Assets/Characters/char_1001_amiya2.png"));
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            parts.AlphaTextureAssetId,
            AssetType.Texture2D,
            "char_1001_amiya2_alpha",
            "Assets/Characters/char_1001_amiya2_alpha.png"));
        return parts;
    }

    private static void RegisterSpineGroupPreviews(
        WorkspaceId workspaceId,
        BundleId bundleId,
        SpineGroupAssetIds parts,
        TestAssetPreviewHandler previewHandler)
    {
        previewHandler.RegisterPreview(CreateBinaryPreview(
            workspaceId,
            bundleId,
            parts.SkeletonAssetId,
            AssetPreviewKind.Binary,
            "application/octet-stream",
            [0x1c, 0x00, 0x00, 0x00]));
        previewHandler.RegisterPreview(CreateTextPreview(
            workspaceId,
            bundleId,
            parts.AtlasAssetId,
            "atlas",
            """
            char_1001_amiya2.png
            size: 512,512
            format: RGBA8888
            filter: Linear,Linear
            repeat: none
            body
              bounds: 0,0,128,128
              rotate: false
            """));
        previewHandler.RegisterPreview(CreateBinaryPreview(
            workspaceId,
            bundleId,
            parts.TextureAssetId,
            AssetPreviewKind.Image,
            "image/png",
            [1, 2, 3]));
        previewHandler.RegisterPreview(CreateBinaryPreview(
            workspaceId,
            bundleId,
            parts.AlphaTextureAssetId,
            AssetPreviewKind.Image,
            "image/png",
            [4, 5, 6]));
    }

    private sealed record SpineGroupAssetIds(
        AssetId SkeletonAssetId,
        AssetId AtlasAssetId,
        AssetId TextureAssetId,
        AssetId AlphaTextureAssetId);

    private const string WorkspaceKey = "workspace-a";
    private const string WorkspaceDataRootPath = @"D:\WorkspaceData\A";

    private static ExtractAssetHandler CreateHandler(
        TestWorkspaceSessionRegistry registry,
        TestAssetPreviewHandler previewHandler,
        TestAssetExtractionFileStore fileStore,
        TestWorkspaceChangeStore changeStore)
    {
        var exporter = new WorkspaceAssetExtractionExporter(previewHandler, fileStore);
        var recorder = new WorkspaceExtractionOperationRecorder(
            new TestWorkspaceDataStoreFactory(changeStore),
            new WorkspaceOperationLogService());
        return new ExtractAssetHandler(registry, exporter, recorder);
    }

    private static AssetDetailsDto CreateAssetDetails(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        AssetType assetType,
        string assetName,
        string assetPath,
        string? assetTypeDisplayName = null,
        long pathId = 12345) =>
        new(
            WorkspaceId: workspaceId,
            BundleId: bundleId,
            AssetId: assetId,
            Name: assetName,
            AssetPath: assetPath,
            ContainerPath: string.Empty,
            AssetType: assetType,
            SizeInBytes: 256,
            PreferredDisplaySizeInBytes: 256,
            PathId: pathId,
            IsModified: false,
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
            Dependencies: Array.Empty<AssetDependencyDto>(),
            AssetTypeDisplayName: assetTypeDisplayName ?? assetType.ToString(),
            SerializedFileName: "bundle.assets");

    private static AssetPreviewDto CreateTextPreview(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        string displayText,
        string fullText) =>
        new(
            WorkspaceId: workspaceId,
            BundleId: bundleId,
            AssetId: assetId,
            Kind: AssetPreviewKind.Text,
            ContentType: "text/plain; charset=utf-8",
            BinaryContent: null,
            TextContent: displayText,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: CreateDiagnostic())
        {
            FullTextContent = fullText
        };

    private static AssetPreviewDto CreateBinaryPreview(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        AssetPreviewKind kind,
        string contentType,
        byte[] content) =>
        new(
            WorkspaceId: workspaceId,
            BundleId: bundleId,
            AssetId: assetId,
            Kind: kind,
            ContentType: contentType,
            BinaryContent: content,
            TextContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: content.Length,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: CreateDiagnostic());

    private static AssetPreviewDiagnosticDto CreateDiagnostic() =>
        new(
            AssetPreviewCacheStatusKind.Missing,
            AssetPreviewFallbackReasonKind.None);

    private static void RecordMaximumConcurrency(ref int target, int candidate)
    {
        while (true)
        {
            var current = Volatile.Read(ref target);
            if (candidate <= current)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref target, candidate, current) == current)
            {
                return;
            }
        }
    }

    private sealed class RecordingProgress<T> : IProgress<T>
    {
        private readonly object _syncRoot = new();
        private readonly List<T> _reports = [];

        public IReadOnlyList<T> Reports
        {
            get
            {
                lock (_syncRoot)
                {
                    return _reports.ToArray();
                }
            }
        }

        public void Report(T value)
        {
            lock (_syncRoot)
            {
                _reports.Add(value);
            }
        }
    }

    private sealed class TestAssetPreviewHandler : IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>>
    {
        private readonly Dictionary<(string BundleId, string AssetId), AssetPreviewDto> _previews = new();
        private readonly object _syncRoot = new();

        public GetAssetPreviewQuery? LastQuery { get; private set; }

        public Func<GetAssetPreviewQuery, CancellationToken, Task>? BeforeReturnAsync { get; init; }

        public void RegisterPreview(AssetPreviewDto preview)
        {
            lock (_syncRoot)
            {
                _previews[(preview.BundleId.Value, preview.AssetId.Value)] = preview;
            }
        }

        public async Task<Result<AssetPreviewDto>> HandleAsync(
            GetAssetPreviewQuery query,
            CancellationToken cancellationToken = default)
        {
            AssetPreviewDto? preview;
            lock (_syncRoot)
            {
                LastQuery = query;
                _previews.TryGetValue((query.BundleId.Value, query.AssetId.Value), out preview);
            }

            if (BeforeReturnAsync is not null)
            {
                await BeforeReturnAsync(query, cancellationToken);
            }

            return preview is not null
                ? Result<AssetPreviewDto>.Success(preview)
                : Result<AssetPreviewDto>.Failure(ApplicationErrors.AssetNotFound(query.AssetId));
        }
    }

    private sealed class TestAssetExtractionFileStore : IAssetExtractionFileStore
    {
        private readonly object _syncRoot = new();

        public List<AssetExtractionWriteRequestDto> Requests { get; } = [];

        public AssetExtractionWriteResultDto? LastResult { get; private set; }

        public Task<Result<AssetExtractionWriteResultDto>> WriteAsync(
            AssetExtractionWriteRequestDto request,
            CancellationToken cancellationToken = default)
        {
            lock (_syncRoot)
            {
                Requests.Add(request);
                LastResult = new AssetExtractionWriteResultDto(
                    OutputPath: $@"D:\Exports\{Path.GetFileName(request.SuggestedOutputPath)}",
                    OutputDirectory: @"D:\Exports",
                    OutputSizeInBytes: request.Content?.Length ?? 16,
                    OutputFileExists: true);
                return Task.FromResult(Result<AssetExtractionWriteResultDto>.Success(LastResult));
            }
        }
    }

    private sealed class TestWorkspaceExportManifestStore : IWorkspaceExportManifestStore
    {
        public WorkspaceExportManifestSaveRequestDto? LastRequest { get; private set; }

        public Task<Result<WorkspaceExportManifestSaveResultDto>> SaveAsync(
            WorkspaceExportManifestSaveRequestDto request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var items = request.Items
                .Select(source => new WorkspaceExportManifestItemDto(
                    RelativePath: Path.GetFileName(source.OutputPath),
                    BundleId: source.BundleId.Value,
                    BundleDisplayName: source.BundleDisplayName,
                    AssetId: source.AssetId.Value,
                    PathId: source.PathId,
                    AssetType: source.AssetType,
                    AssetTypeDisplayName: source.AssetTypeDisplayName,
                    OriginalName: source.OriginalName,
                    DisambiguatedName: source.DisambiguatedName,
                    ContainerPath: source.ContainerPath,
                    OutputRole: source.OutputRole,
                    SpineGroupId: source.SpineGroupId,
                    SpineGroupDisplayName: source.SpineGroupDisplayName,
                    ExportedFileSizeInBytes: 3,
                    ExportedFileHashAlgorithm: "MD5",
                    ExportedFileHash: "5289df737df57326fcdd22597afb1fac",
                    ExportedFileLastWriteTimeUtc: request.ExportedAtUtc))
                .ToArray();
            var manifest = new WorkspaceExportManifestDto(
                SchemaVersion: 1,
                WorkspaceKey: request.WorkspaceKey,
                ExportedAtUtc: request.ExportedAtUtc,
                OutputRoot: request.OutputRoot,
                Items: items);
            return Task.FromResult(Result<WorkspaceExportManifestSaveResultDto>.Success(
                new WorkspaceExportManifestSaveResultDto(
                    Path.Combine(request.OutputRoot, "_abt-export-manifest.json"),
                    manifest)));
        }

        public Task<Result<WorkspaceExportManifestDto>> LoadAsync(
            string manifestPath,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestWorkspaceDataStoreFactory : IWorkspaceDataStoreFactory
    {
        private readonly TestWorkspaceChangeStore _changeStore;

        public TestWorkspaceDataStoreFactory(TestWorkspaceChangeStore changeStore)
        {
            _changeStore = changeStore;
        }

        public IBundleArtifactStore CreateBundleArtifactStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IBundleIndexCacheStore CreateBundleIndexCacheStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IPreviewCacheStore CreatePreviewCacheStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IWorkspaceChangeStore CreateWorkspaceChangeStore(string? rootDirectoryPath = null)
        {
            _changeStore.LastRootDirectoryPath = rootDirectoryPath;
            return _changeStore;
        }

        public string GetDefaultRootDirectoryPath() => WorkspaceDataRootPath;
    }

    private sealed class TestWorkspaceChangeStore : IWorkspaceChangeStore
    {
        public WorkspaceChangeSetDto? ChangeSet { get; init; }

        public WorkspaceChangeSetDto? SavedChangeSet { get; private set; }

        public string? LastLoadedWorkspaceKey { get; private set; }

        public string? LastRootDirectoryPath { get; set; }

        public WorkspaceChangeSetDto? Load(string workspaceKey)
        {
            LastLoadedWorkspaceKey = workspaceKey;
            return SavedChangeSet ?? ChangeSet;
        }

        public void Save(WorkspaceChangeSetDto changeSet)
        {
            lock (this)
            {
                SavedChangeSet = changeSet;
            }
        }

        public void Delete(string workspaceKey) =>
            throw new NotSupportedException();
    }

    private sealed class TestWorkspaceSessionRegistry : IWorkspaceSessionRegistry
    {
        private readonly object _syncRoot = new();
        private readonly WorkspaceId _workspaceId;
        private readonly Dictionary<(string BundleId, string AssetId), AssetDetailsDto> _assetDetails = new();

        public TestWorkspaceSessionRegistry(WorkspaceId workspaceId)
        {
            _workspaceId = workspaceId;
        }

        public HashSet<(string BundleId, string AssetId)> ExtractedAssets { get; } = [];

        public void RegisterAsset(AssetDetailsDto details)
        {
            lock (_syncRoot)
            {
                _assetDetails[(details.BundleId.Value, details.AssetId.Value)] = details;
            }
        }

        public WorkspaceSummaryDto OpenWorkspace(WorkspaceSessionOpenRequestDto request) =>
            throw new NotSupportedException();

        public bool ContainsWorkspace(WorkspaceId workspaceId) => workspaceId.Equals(_workspaceId);

        public bool ContainsBundle(WorkspaceId workspaceId, BundleId bundleId) =>
            _assetDetails.Keys.Any(key => string.Equals(key.BundleId, bundleId.Value, StringComparison.Ordinal));

        public bool ContainsAsset(WorkspaceId workspaceId, BundleId bundleId, AssetId assetId) =>
            _assetDetails.ContainsKey((bundleId.Value, assetId.Value));

        public bool TryGetBundleTree(WorkspaceId workspaceId, out IReadOnlyList<BundleTreeNodeDto> bundleTree)
        {
            bundleTree = _assetDetails.Values
                .GroupBy(static asset => asset.BundleId.Value)
                .Select(group => new BundleTreeNodeDto(
                    BundleId: new BundleId(group.Key),
                    DisplayName: $"{group.Key}.ab",
                    LogicalPath: $"{group.Key}.ab",
                    SourcePath: $@"C:\Bundles\{group.Key}.ab",
                    IsVirtualFolder: false,
                    AssetCount: group.Count(),
                    Children: Array.Empty<BundleTreeNodeDto>()))
                .ToArray();
            return true;
        }

        public bool TryGetAssetList(
            WorkspaceId workspaceId,
            BundleId bundleId,
            string? searchKeyword,
            AssetType? assetTypeFilter,
            string? assetTypeDisplayNameFilter,
            out AssetListResultDto? assetListResult)
        {
            var items = _assetDetails.Values
                .Where(asset => asset.BundleId.Equals(bundleId))
                .Where(asset => assetTypeFilter is null || asset.AssetType == assetTypeFilter)
                .Where(asset =>
                    string.IsNullOrWhiteSpace(assetTypeDisplayNameFilter) ||
                    string.Equals(
                        asset.AssetTypeDisplayName,
                        assetTypeDisplayNameFilter.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                .Select(asset => new AssetListItemDto(
                    AssetId: asset.AssetId,
                    BundleId: asset.BundleId,
                    Name: asset.Name,
                    AssetPath: asset.AssetPath,
                    AssetType: asset.AssetType,
                    SizeInBytes: asset.SizeInBytes,
                    PreferredDisplaySizeInBytes: asset.PreferredDisplaySizeInBytes,
                    HasPreview: true,
                    IsModified: asset.IsModified,
                    AssetTypeDisplayName: asset.AssetTypeDisplayName,
                    PathId: asset.PathId))
                .ToArray();

            assetListResult = new AssetListResultDto(
                WorkspaceId: workspaceId,
                BundleId: bundleId,
                BundleDisplayName: $"{bundleId.Value}.ab",
                SearchKeyword: searchKeyword,
                AssetTypeFilter: assetTypeFilter,
                TotalCount: items.Length,
                FilteredCount: items.Length,
                Items: items,
                AssetTypeDisplayNameFilter: assetTypeDisplayNameFilter);
            return true;
        }

        public bool TryGetAssetDetails(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetDetailsDto? assetDetails) =>
            _assetDetails.TryGetValue((bundleId.Value, assetId.Value), out assetDetails);

        public bool TryGetWorkspaceChangeSummary(
            WorkspaceId workspaceId,
            out WorkspaceChangeSummaryDto? changeSummary)
        {
            changeSummary = null;
            return false;
        }

        public bool TryExtractAsset(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetExtractionResultDto? extractionResult)
        {
            lock (_syncRoot)
            {
                if (!_assetDetails.TryGetValue((bundleId.Value, assetId.Value), out var details))
                {
                    extractionResult = null;
                    return false;
                }

                ExtractedAssets.Add((bundleId.Value, assetId.Value));
                extractionResult = new AssetExtractionResultDto(
                    WorkspaceId: workspaceId,
                    BundleId: bundleId,
                    AssetId: assetId,
                    AssetName: details.Name,
                    SuggestedOutputPath: "legacy-output",
                    ExtractedAtUtc: DateTimeOffset.UtcNow);
                return true;
            }
        }

        public bool TryGetWorkspaceReplacementCandidates(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            out IReadOnlyList<WorkspaceReplacementCandidateDto> candidates) =>
            throw new NotSupportedException();

        public bool TryReplaceAssetFromFile(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            string sourceFilePath,
            AssetType sourceAssetType,
            out AssetFileReplacementResultDto? replacementResult) =>
            throw new NotSupportedException();

        public bool TryReplaceAssetFromWorkspace(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            BundleId sourceBundleId,
            AssetId sourceAssetId,
            out AssetReplacementResultDto? replacementResult) =>
            throw new NotSupportedException();

        public bool TryUndoAssetReplacement(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetReplacementUndoResultDto? undoResult) =>
            throw new NotSupportedException();

        public bool TryOpenAssetInPane(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            PaneId paneId,
            out AssetPaneContextDto? paneContext) =>
            throw new NotSupportedException();
    }
}
