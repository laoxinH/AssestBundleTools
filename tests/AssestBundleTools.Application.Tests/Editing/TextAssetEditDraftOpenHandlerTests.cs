using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Editing.Commands;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Editing;

internal static class TextAssetEditDraftOpenHandlerTests
{
    public static async Task OpenTextAssetEditDraftHandler_ShouldReturnExistingDraftReference()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        const string workspaceKey = "workspace-a";
        const string workspaceDataRootPath = @"D:\WorkspaceData\A";
        var bundleId = new BundleId("bundle.text");
        var assetId = new AssetId("asset.text");
        var updatedAtUtc = new DateTimeOffset(2026, 7, 4, 10, 0, 0, TimeSpan.Zero);
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.TextAsset,
            "story.json",
            textEditDraftId: "draft-1",
            textEditDraftMode: WorkspaceTextEditOperationRecorder.TextDraftMode,
            textEditDraftRevision: 2));
        var changeStore = new TestWorkspaceChangeStore
        {
            ChangeSet = new WorkspaceChangeSetDto(
                WorkspaceKey: workspaceKey,
                UpdatedAtUtc: updatedAtUtc,
                Changes: Array.Empty<WorkspaceChangeRecordDto>(),
                EditDrafts:
                [
                    new WorkspaceAssetEditDraftDescriptorDto(
                        DraftId: " draft-1 ",
                        DraftMode: $" {WorkspaceTextEditOperationRecorder.TextDraftMode} ",
                        SourceFingerprint: "source-v1",
                        UpdatedAtUtc: updatedAtUtc,
                        SourceDescription: "story.json",
                        Summary: "TextAsset")
                ],
                Operations: Array.Empty<WorkspaceOperationRecordDto>())
        };
        var draftTextStore = new TestAssetEditDraftTextStore
        {
            Chunk = CreateChunk(workspaceKey, "draft-1", WorkspaceTextEditOperationRecorder.TextDraftMode, "edited")
        };
        var dataStoreFactory = new TestWorkspaceDataStoreFactory(changeStore, draftTextStore);
        var handler = new OpenTextAssetEditDraftHandler(registry, dataStoreFactory);

        var result = await handler.HandleAsync(new OpenTextAssetEditDraftCommand(
            workspaceId,
            workspaceKey,
            workspaceDataRootPath,
            bundleId,
            assetId,
            "draft-1",
            WorkspaceTextEditOperationRecorder.TextDraftMode));

        TestAssert.True(result.IsSuccess, "Existing TextAsset draft should open successfully.");
        var draft = result.Value ?? throw new InvalidOperationException("Expected draft result.");
        TestAssert.Equal("draft-1", draft.DraftId, "Opened draft should keep the existing draft id.");
        TestAssert.Equal(WorkspaceTextEditOperationRecorder.TextDraftMode, draft.DraftMode, "Opened draft should keep the existing draft mode.");
        TestAssert.Equal(AssetPreviewFullContentReferenceKind.EditDraft, draft.Reference.Kind, "Opened draft should return an edit draft reference.");
        TestAssert.Equal("draft-1", draft.Reference.DraftId, "Reference draft id should match.");
        TestAssert.Equal(WorkspaceTextEditOperationRecorder.TextDraftMode, draft.Reference.DraftMode, "Reference draft mode should match.");
        TestAssert.Equal(2, draft.Reference.DraftRevision, "Reference should pin the current applied draft revision.");
        TestAssert.Equal(6L, draft.Reference.ContentLength, "Reference should reflect draft payload length.");
        TestAssert.True(draft.CanEditInline, "Small TextAsset draft should be inline editable.");
        TestAssert.True(draft.IsAppliedBaseline, "Current applied text edit draft should be marked as an applied baseline.");
        TestAssert.Equal(2, draftTextStore.LastChunkRevision, "Opening an applied draft should read the current revision instead of the draft head.");
        TestAssert.Equal("source-v1", draft.SourceFingerprint, "Opened draft should expose descriptor source fingerprint.");
        TestAssert.Equal("story.json", draft.SourceDescription, "Opened draft should expose descriptor source description.");
        TestAssert.Equal(workspaceDataRootPath, dataStoreFactory.LastChangeStoreRootPath, "Handler should use requested data root for change set.");
        TestAssert.Equal(workspaceDataRootPath, dataStoreFactory.LastDraftStoreRootPath, "Handler should use requested data root for draft payload.");
        if (changeStore.SavedChangeSet is not null)
        {
            throw new InvalidOperationException("Opening an existing draft must not append new operations.");
        }
    }

    public static async Task OpenTextAssetEditDraftHandler_ShouldUseRequestedDraftRevision_WhenAppliedBaselineIsPinned()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        const string workspaceKey = "workspace-a";
        const string workspaceDataRootPath = @"D:\WorkspaceData\A";
        var bundleId = new BundleId("bundle.text");
        var assetId = new AssetId("asset.text");
        var updatedAtUtc = new DateTimeOffset(2026, 7, 4, 10, 0, 0, TimeSpan.Zero);
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.TextAsset,
            "story.json",
            textEditDraftId: "draft-1",
            textEditDraftMode: WorkspaceTextEditOperationRecorder.TextDraftMode,
            textEditDraftRevision: null));
        var changeStore = new TestWorkspaceChangeStore
        {
            ChangeSet = new WorkspaceChangeSetDto(
                WorkspaceKey: workspaceKey,
                UpdatedAtUtc: updatedAtUtc,
                Changes: Array.Empty<WorkspaceChangeRecordDto>(),
                EditDrafts:
                [
                    new WorkspaceAssetEditDraftDescriptorDto(
                        DraftId: "draft-1",
                        DraftMode: WorkspaceTextEditOperationRecorder.TextDraftMode,
                        SourceFingerprint: "source-v1",
                        UpdatedAtUtc: updatedAtUtc,
                        SourceDescription: "story.json",
                        Summary: "TextAsset")
                ],
                Operations: Array.Empty<WorkspaceOperationRecordDto>())
        };
        var draftTextStore = new TestAssetEditDraftTextStore
        {
            Chunk = CreateChunk(workspaceKey, "draft-1", WorkspaceTextEditOperationRecorder.TextDraftMode, "edited")
        };
        var handler = new OpenTextAssetEditDraftHandler(
            registry,
            new TestWorkspaceDataStoreFactory(changeStore, draftTextStore));

        var result = await handler.HandleAsync(new OpenTextAssetEditDraftCommand(
            workspaceId,
            workspaceKey,
            workspaceDataRootPath,
            bundleId,
            assetId,
            "draft-1",
            WorkspaceTextEditOperationRecorder.TextDraftMode,
            SourceFingerprint: null,
            DraftRevision: 3));

        TestAssert.True(result.IsSuccess, "Pinned applied draft should open successfully.");
        TestAssert.Equal(3, draftTextStore.LastChunkRevision, "Requested applied baseline revision should be used when the current projection has no revision.");
        TestAssert.Equal(3, result.Value?.Reference.DraftRevision, "Returned reference should preserve the requested applied baseline revision.");
    }

    public static async Task OpenTextAssetEditDraftHandler_ShouldFail_WhenPayloadIsMissing()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        const string workspaceKey = "workspace-a";
        var bundleId = new BundleId("bundle.text");
        var assetId = new AssetId("asset.text");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.TextAsset,
            "story.json"));
        var changeStore = new TestWorkspaceChangeStore
        {
            ChangeSet = new WorkspaceChangeSetDto(
                WorkspaceKey: workspaceKey,
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                Changes: Array.Empty<WorkspaceChangeRecordDto>(),
                EditDrafts:
                [
                    new WorkspaceAssetEditDraftDescriptorDto(
                        "draft-missing-payload",
                        WorkspaceTextEditOperationRecorder.TextDraftMode,
                        "source-v1",
                        DateTimeOffset.UtcNow)
                ],
                Operations: Array.Empty<WorkspaceOperationRecordDto>())
        };
        var handler = new OpenTextAssetEditDraftHandler(
            registry,
            new TestWorkspaceDataStoreFactory(changeStore, new TestAssetEditDraftTextStore()));

        var result = await handler.HandleAsync(new OpenTextAssetEditDraftCommand(
            workspaceId,
            workspaceKey,
            @"D:\WorkspaceData\A",
            bundleId,
            assetId,
            "draft-missing-payload",
            WorkspaceTextEditOperationRecorder.TextDraftMode));

        TestAssert.True(result.IsFailure, "Missing draft payload should fail instead of creating a new draft.");
        TestAssert.Equal(
            "Asset.Preview.FullContentUnavailable",
            TestAssert.Single(result.Errors).Code,
            "Missing payload should return a full-content unavailable error.");
    }

    public static async Task OpenTextAssetEditDraftHandler_ShouldRestoreDeferredSourceReference_WhenPayloadIsMissing()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        const string workspaceKey = "workspace-a";
        var bundleId = new BundleId("bundle.text");
        var assetId = new AssetId("asset.text");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.TextAsset,
            "large.atlas"));
        var sourceReference = new WorkspaceAssetEditDraftSourceReferenceDto(
            nameof(AssetPreviewFullContentReferenceKind.PreviewCache),
            "text/plain; charset=utf-8",
            12 * 1024 * 1024,
            workspaceKey,
            PreviewCacheKey: "preview-large-text");
        var changeStore = new TestWorkspaceChangeStore
        {
            ChangeSet = new WorkspaceChangeSetDto(
                WorkspaceKey: workspaceKey,
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                Changes: Array.Empty<WorkspaceChangeRecordDto>(),
                EditDrafts:
                [
                    new WorkspaceAssetEditDraftDescriptorDto(
                        "draft-deferred",
                        WorkspaceTextEditOperationRecorder.TextDraftMode,
                        "source-v1",
                        DateTimeOffset.UtcNow,
                        SourceReference: sourceReference,
                        IsPayloadMaterialized: false)
                ],
                Operations: Array.Empty<WorkspaceOperationRecordDto>())
        };
        var handler = new OpenTextAssetEditDraftHandler(
            registry,
            new TestWorkspaceDataStoreFactory(changeStore, new TestAssetEditDraftTextStore()));

        var result = await handler.HandleAsync(new OpenTextAssetEditDraftCommand(
            workspaceId,
            workspaceKey,
            @"D:\WorkspaceData\A",
            bundleId,
            assetId,
            "draft-deferred",
            WorkspaceTextEditOperationRecorder.TextDraftMode,
            "source-v1"));

        TestAssert.True(result.IsSuccess, "Deferred text draft should reopen from its source reference without a materialized payload.");
        var draft = result.Value ?? throw new InvalidOperationException("Expected draft result.");
        TestAssert.Equal(AssetPreviewFullContentReferenceKind.PreviewCache, draft.Reference.Kind, "Source reference kind should be restored.");
        TestAssert.Equal("preview-large-text", draft.Reference.PreviewCacheKey, "Preview cache key should be restored.");
        TestAssert.False(draft.IsMaterialized, "Deferred source reference should not be marked as materialized.");
        TestAssert.True(draft.CanEditInline, "Large but budgeted TextAsset can still be loaded for full editing on demand.");
    }

    public static async Task OpenTextAssetEditDraftHandler_ShouldFail_WhenMaterializedPayloadIsMissing()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        const string workspaceKey = "workspace-a";
        var bundleId = new BundleId("bundle.text");
        var assetId = new AssetId("asset.text");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.TextAsset,
            "edited.atlas"));
        var sourceReference = new WorkspaceAssetEditDraftSourceReferenceDto(
            nameof(AssetPreviewFullContentReferenceKind.PreviewCache),
            "text/plain; charset=utf-8",
            12 * 1024 * 1024,
            workspaceKey,
            PreviewCacheKey: "preview-original-text");
        var changeStore = new TestWorkspaceChangeStore
        {
            ChangeSet = new WorkspaceChangeSetDto(
                WorkspaceKey: workspaceKey,
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                Changes: Array.Empty<WorkspaceChangeRecordDto>(),
                EditDrafts:
                [
                    new WorkspaceAssetEditDraftDescriptorDto(
                        "draft-materialized",
                        WorkspaceTextEditOperationRecorder.TextDraftMode,
                        "source-v1",
                        DateTimeOffset.UtcNow,
                        SourceReference: sourceReference,
                        IsPayloadMaterialized: true)
                ],
                Operations: Array.Empty<WorkspaceOperationRecordDto>())
        };
        var handler = new OpenTextAssetEditDraftHandler(
            registry,
            new TestWorkspaceDataStoreFactory(changeStore, new TestAssetEditDraftTextStore()));

        var result = await handler.HandleAsync(new OpenTextAssetEditDraftCommand(
            workspaceId,
            workspaceKey,
            @"D:\WorkspaceData\A",
            bundleId,
            assetId,
            "draft-materialized",
            WorkspaceTextEditOperationRecorder.TextDraftMode,
            "source-v1"));

        TestAssert.True(result.IsFailure, "Materialized draft should not fall back to the original source reference when its payload is missing.");
        TestAssert.Equal(
            "Asset.Preview.FullContentUnavailable",
            TestAssert.Single(result.Errors).Code,
            "Missing materialized payload should remain a full-content unavailable error.");
    }

    private static PreviewCacheTextChunkDto CreateChunk(
        string workspaceKey,
        string draftId,
        string draftMode,
        string text)
    {
        var descriptor = new PreviewCacheDescriptorDto(
            WorkspaceKey: workspaceKey,
            PreviewCacheKey: $"draft.{draftMode}.{draftId}",
            BundleCacheKey: $"draft:{draftId}",
            AssetId: null,
            PreviewKind: AssetPreviewKind.Text.ToString(),
            ContentType: "text/plain; charset=utf-8",
            RelativePayloadPath: "text.txt",
            SourceFingerprint: new WorkspaceFileFingerprintDto(
                $"draft:{draftId}:{draftMode}",
                text.Length,
                DateTimeOffset.UtcNow,
                "hash"),
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            GeneratorVersion: "test",
            PreviewInfoItems:
            [
                new AssetPreviewInfoItemDto(
                    AssetPreviewInfoKey.TextFullCharacterCount,
                    AssetPreviewInfoValueKind.Integer,
                    Int64Value: text.Length)
            ]);

        return new PreviewCacheTextChunkDto(
            descriptor,
            text,
            Offset: 0,
            Length: text.Length,
            TotalLength: text.Length,
            ContentLength: text.Length);
    }

    private static AssetDetailsDto CreateAssetDetails(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        AssetType assetType,
        string assetName,
        string? textEditDraftId = null,
        string? textEditDraftMode = null,
        int? textEditDraftRevision = null) =>
        new(
            WorkspaceId: workspaceId,
            BundleId: bundleId,
            AssetId: assetId,
            Name: assetName,
            AssetPath: $"Assets/{assetName}",
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
            TextEditDraftId: textEditDraftId,
            TextEditDraftMode: textEditDraftMode,
            TextEditDraftRevision: textEditDraftRevision);

    private sealed class TestWorkspaceDataStoreFactory : IWorkspaceDataStoreFactory
    {
        private readonly TestWorkspaceChangeStore _changeStore;
        private readonly TestAssetEditDraftTextStore _draftTextStore;

        public TestWorkspaceDataStoreFactory(
            TestWorkspaceChangeStore changeStore,
            TestAssetEditDraftTextStore draftTextStore)
        {
            _changeStore = changeStore;
            _draftTextStore = draftTextStore;
        }

        public string? LastChangeStoreRootPath { get; private set; }

        public string? LastDraftStoreRootPath { get; private set; }

        public IBundleArtifactStore CreateBundleArtifactStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IBundleIndexCacheStore CreateBundleIndexCacheStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IPreviewCacheStore CreatePreviewCacheStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IWorkspaceChangeStore CreateWorkspaceChangeStore(string? rootDirectoryPath = null)
        {
            LastChangeStoreRootPath = rootDirectoryPath;
            return _changeStore;
        }

        public IAssetEditDraftTextStore CreateAssetEditDraftTextStore(string? rootDirectoryPath = null)
        {
            LastDraftStoreRootPath = rootDirectoryPath;
            return _draftTextStore;
        }

        public string GetDefaultRootDirectoryPath() => @"D:\WorkspaceData";
    }

    private sealed class TestWorkspaceChangeStore : IWorkspaceChangeStore
    {
        public WorkspaceChangeSetDto? ChangeSet { get; init; }

        public WorkspaceChangeSetDto? SavedChangeSet { get; private set; }

        public WorkspaceChangeSetDto? Load(string workspaceKey) => ChangeSet;

        public void Save(WorkspaceChangeSetDto changeSet)
        {
            SavedChangeSet = changeSet;
        }

        public void Delete(string workspaceKey) =>
            throw new NotSupportedException();
    }

    private sealed class TestAssetEditDraftTextStore : IAssetEditDraftTextStore
    {
        public PreviewCacheTextChunkDto? Chunk { get; init; }

        public int? LastChunkRevision { get; private set; }

        public PreviewCacheTextChunkDto? LoadTextChunk(
            string workspaceKey,
            string draftId,
            string draftMode,
            int offset,
            int length,
            int? revision = null)
        {
            LastChunkRevision = revision;
            return Chunk;
        }

        public PreviewCacheTextLineRangeDto? LoadTextLines(
            string workspaceKey,
            string draftId,
            string draftMode,
            int startLineIndex,
            int lineCount,
            int? revision = null) =>
            throw new NotSupportedException();

        public PreviewCacheTextSearchResultDto? SearchText(
            string workspaceKey,
            string draftId,
            string draftMode,
            string searchText,
            int startLineIndex,
            int maxMatchCount,
            bool matchCase,
            int? revision = null) =>
            throw new NotSupportedException();

        public void SaveTextPayload(AssetEditDraftTextPayloadDto payload) =>
            throw new NotSupportedException();

        public AssetEditDraftTextPatchResultDto ApplyTextPatch(AssetEditDraftTextPatchDto patch) =>
            throw new NotSupportedException();

        public void Delete(string workspaceKey, string draftId, string draftMode) =>
            throw new NotSupportedException();
    }

    private sealed class TestWorkspaceSessionRegistry : IWorkspaceSessionRegistry
    {
        private readonly Dictionary<(string BundleId, string AssetId), AssetDetailsDto> _assetDetails = new();

        public void RegisterAssetDetails(AssetDetailsDto details)
        {
            _assetDetails[(details.BundleId.Value, details.AssetId.Value)] = details;
        }

        public WorkspaceSummaryDto OpenWorkspace(WorkspaceSessionOpenRequestDto request) =>
            throw new NotSupportedException();

        public bool ContainsWorkspace(WorkspaceId workspaceId) => true;

        public bool ContainsBundle(WorkspaceId workspaceId, BundleId bundleId) => true;

        public bool ContainsAsset(WorkspaceId workspaceId, BundleId bundleId, AssetId assetId) =>
            _assetDetails.ContainsKey((bundleId.Value, assetId.Value));

        public bool TryGetAssetDetails(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetDetailsDto? assetDetails) =>
            _assetDetails.TryGetValue((bundleId.Value, assetId.Value), out assetDetails);

        public bool TryGetBundleTree(WorkspaceId workspaceId, out IReadOnlyList<BundleTreeNodeDto> bundleTree) =>
            throw new NotSupportedException();

        public bool TryGetAssetList(
            WorkspaceId workspaceId,
            BundleId bundleId,
            string? searchKeyword,
            AssetType? assetTypeFilter,
            string? assetTypeDisplayNameFilter,
            out AssetListResultDto? assetListResult) =>
            throw new NotSupportedException();

        public bool TryGetWorkspaceChangeSummary(
            WorkspaceId workspaceId,
            out WorkspaceChangeSummaryDto? changeSummary) =>
            throw new NotSupportedException();

        public bool TryExtractAsset(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetExtractionResultDto? extractionResult) =>
            throw new NotSupportedException();

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
