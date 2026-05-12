using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
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

    public static async Task ExtractSpineAssetGroupHandler_ShouldExtractSkeletonAtlasTextureAndAlphaPages()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.char_1001_amiya2");
        var skeletonAssetId = new AssetId("asset.skel");
        var atlasAssetId = new AssetId("asset.atlas");
        var textureAssetId = new AssetId("asset.texture");
        var alphaTextureAssetId = new AssetId("asset.texture.alpha");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            skeletonAssetId,
            AssetType.TextAsset,
            "char_1001_amiya2.skel.bytes",
            "Assets/Characters/char_1001_amiya2.skel.bytes"));
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            atlasAssetId,
            AssetType.TextAsset,
            "char_1001_amiya2.atlas",
            "Assets/Characters/char_1001_amiya2.atlas"));
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            textureAssetId,
            AssetType.Texture2D,
            "char_1001_amiya2",
            "Assets/Characters/char_1001_amiya2.png"));
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            alphaTextureAssetId,
            AssetType.Texture2D,
            "char_1001_amiya2_alpha",
            "Assets/Characters/char_1001_amiya2_alpha.png"));

        var previewHandler = new TestAssetPreviewHandler();
        previewHandler.RegisterPreview(CreateBinaryPreview(
            workspaceId,
            bundleId,
            skeletonAssetId,
            AssetPreviewKind.Binary,
            "application/octet-stream",
            [0x1c, 0x00, 0x00, 0x00]));
        previewHandler.RegisterPreview(CreateTextPreview(
            workspaceId,
            bundleId,
            atlasAssetId,
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
            textureAssetId,
            AssetPreviewKind.Image,
            "image/png",
            [1, 2, 3]));
        previewHandler.RegisterPreview(CreateBinaryPreview(
            workspaceId,
            bundleId,
            alphaTextureAssetId,
            AssetPreviewKind.Image,
            "image/png",
            [4, 5, 6]));

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
            requestedPaths.Any(static path => path.EndsWith("char_1001_amiya2.skel.bytes", StringComparison.OrdinalIgnoreCase)),
            "The skeleton payload should keep its .skel.bytes extension.");
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
        string? assetTypeDisplayName = null) =>
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
            PathId: 12345,
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

    private sealed class TestAssetPreviewHandler : IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>>
    {
        private readonly Dictionary<(string BundleId, string AssetId), AssetPreviewDto> _previews = new();

        public GetAssetPreviewQuery? LastQuery { get; private set; }

        public void RegisterPreview(AssetPreviewDto preview)
        {
            _previews[(preview.BundleId.Value, preview.AssetId.Value)] = preview;
        }

        public Task<Result<AssetPreviewDto>> HandleAsync(
            GetAssetPreviewQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(_previews.TryGetValue((query.BundleId.Value, query.AssetId.Value), out var preview)
                ? Result<AssetPreviewDto>.Success(preview)
                : Result<AssetPreviewDto>.Failure(ApplicationErrors.AssetNotFound(query.AssetId)));
        }
    }

    private sealed class TestAssetExtractionFileStore : IAssetExtractionFileStore
    {
        public List<AssetExtractionWriteRequestDto> Requests { get; } = [];

        public AssetExtractionWriteResultDto? LastResult { get; private set; }

        public Task<Result<AssetExtractionWriteResultDto>> WriteAsync(
            AssetExtractionWriteRequestDto request,
            CancellationToken cancellationToken = default)
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

    private sealed class TestWorkspaceDataStoreFactory : IWorkspaceDataStoreFactory
    {
        private readonly TestWorkspaceChangeStore _changeStore;

        public TestWorkspaceDataStoreFactory(TestWorkspaceChangeStore changeStore)
        {
            _changeStore = changeStore;
        }

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
            SavedChangeSet = changeSet;
        }

        public void Delete(string workspaceKey) =>
            throw new NotSupportedException();
    }

    private sealed class TestWorkspaceSessionRegistry : IWorkspaceSessionRegistry
    {
        private readonly WorkspaceId _workspaceId;
        private readonly Dictionary<(string BundleId, string AssetId), AssetDetailsDto> _assetDetails = new();

        public TestWorkspaceSessionRegistry(WorkspaceId workspaceId)
        {
            _workspaceId = workspaceId;
        }

        public HashSet<(string BundleId, string AssetId)> ExtractedAssets { get; } = [];

        public void RegisterAsset(AssetDetailsDto details)
        {
            _assetDetails[(details.BundleId.Value, details.AssetId.Value)] = details;
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
                    AssetTypeDisplayName: asset.AssetTypeDisplayName))
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
