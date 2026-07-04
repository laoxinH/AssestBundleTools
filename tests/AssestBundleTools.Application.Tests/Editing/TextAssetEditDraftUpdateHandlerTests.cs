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

internal static class TextAssetEditDraftUpdateHandlerTests
{
    public static async Task UpdateTextAssetEditDraftHandler_ShouldSavePayload_AndAppendUpdatedOperation()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        const string workspaceKey = "workspace-a";
        const string workspaceDataRootPath = @"D:\WorkspaceData\A";
        var bundleId = new BundleId("bundle.text");
        var assetId = new AssetId("asset.text");
        const string existingFingerprint = "source-fingerprint-v1";
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
                UpdatedAtUtc: new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
                Changes: Array.Empty<WorkspaceChangeRecordDto>(),
                EditDrafts:
                [
                    new WorkspaceAssetEditDraftDescriptorDto(
                        DraftId: " draft-1 ",
                        DraftMode: $" {WorkspaceTextEditOperationRecorder.TextDraftMode} ",
                        SourceFingerprint: existingFingerprint,
                        UpdatedAtUtc: new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero))
                ],
                Operations: Array.Empty<WorkspaceOperationRecordDto>())
        };
        var draftTextStore = new TestAssetEditDraftTextStore();
        var dataStoreFactory = new TestWorkspaceDataStoreFactory(changeStore, draftTextStore);
        var recorder = new WorkspaceTextEditOperationRecorder(dataStoreFactory, new WorkspaceOperationLogService());
        var handler = new UpdateTextAssetEditDraftHandler(registry, dataStoreFactory, recorder);

        var result = await handler.HandleAsync(new UpdateTextAssetEditDraftCommand(
            workspaceId,
            workspaceKey,
            workspaceDataRootPath,
            bundleId,
            assetId,
            "draft-1",
            WorkspaceTextEditOperationRecorder.TextDraftMode,
            "line 1\r\nline 2"));

        TestAssert.True(result.IsSuccess, "Small TextAsset draft update should succeed.");
        var payload = draftTextStore.LastSavedPayload ?? throw new InvalidOperationException("Expected saved payload.");
        TestAssert.Equal(workspaceDataRootPath, dataStoreFactory.LastDraftStoreRootPath, "Handler should use the requested data root for draft payload.");
        TestAssert.Equal(workspaceKey, payload.WorkspaceKey, "Draft payload should keep the workspace key.");
        TestAssert.Equal("draft-1", payload.DraftId, "Draft payload should keep the draft id.");
        TestAssert.Equal(WorkspaceTextEditOperationRecorder.TextDraftMode, payload.DraftMode, "Draft payload should use text-content mode.");
        TestAssert.Equal("line 1\nline 2", payload.TextContent, "Draft payload should normalize CRLF line endings.");
        TestAssert.Equal("text/plain; charset=utf-8", payload.ContentType, "TextAsset draft content type should be text.");

        var savedOperation = TestAssert.Single(changeStore.SavedChangeSet?.Operations ?? Array.Empty<WorkspaceOperationRecordDto>());
        TestAssert.Equal(WorkspaceOperationKind.EditDraftUpdated, savedOperation.OperationKind, "Draft text update should append an update operation.");
        TestAssert.Equal(WorkspaceOperationStatus.Applied, savedOperation.Status, "Draft update operation should be applied.");
        TestAssert.Equal(bundleId.Value, savedOperation.Target?.BundleId, "Operation target bundle should be recorded.");
        TestAssert.Equal(assetId.Value, savedOperation.Target?.AssetId, "Operation target asset should be recorded.");
        TestAssert.Equal("draft", savedOperation.Source?.SourceKind, "Operation source should be a draft.");
        TestAssert.Equal("draft-1", savedOperation.Source?.DraftId, "Operation source draft id should be recorded.");
        TestAssert.Equal(WorkspaceTextEditOperationRecorder.TextDraftMode, savedOperation.Source?.DraftMode, "Operation source draft mode should be recorded.");

        var descriptor = TestAssert.Single(changeStore.SavedChangeSet?.EditDrafts ?? Array.Empty<WorkspaceAssetEditDraftDescriptorDto>());
        TestAssert.Equal("draft-1", descriptor.DraftId, "Draft descriptor should be normalized and upserted.");
        TestAssert.Equal(WorkspaceTextEditOperationRecorder.TextDraftMode, descriptor.DraftMode, "Draft descriptor mode should be normalized.");
        TestAssert.Equal(existingFingerprint, descriptor.SourceFingerprint, "Draft update should preserve the existing source fingerprint.");
    }

    public static async Task UpdateTextAssetEditDraftHandler_ShouldRejectLargeInlinePayload()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.text");
        var assetId = new AssetId("asset.text");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.TextAsset,
            "large.json"));
        var changeStore = new TestWorkspaceChangeStore();
        var draftTextStore = new TestAssetEditDraftTextStore();
        var dataStoreFactory = new TestWorkspaceDataStoreFactory(changeStore, draftTextStore);
        var handler = new UpdateTextAssetEditDraftHandler(registry, dataStoreFactory);

        var result = await handler.HandleAsync(new UpdateTextAssetEditDraftCommand(
            workspaceId,
            "workspace-a",
            @"D:\WorkspaceData\A",
            bundleId,
            assetId,
            "draft-1",
            WorkspaceTextEditOperationRecorder.TextDraftMode,
            new string('x', AssetTextEditPolicy.InlineEditableTextLength + 1)));

        TestAssert.True(result.IsFailure, "Oversized inline text update should be rejected.");
        TestAssert.Equal(
            "Asset.EditDraft.TextLengthExceeded",
            TestAssert.Single(result.Errors).Code,
            "Oversized inline text should return the edit draft length error.");
        TestAssert.Equal(0, draftTextStore.SaveTextPayloadCallCount, "Rejected update should not save draft payload.");
        if (changeStore.SavedChangeSet is not null)
        {
            throw new InvalidOperationException("Rejected update should not save the workspace change set.");
        }
    }

    private static AssetDetailsDto CreateAssetDetails(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        AssetType assetType,
        string assetName) =>
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
            Dependencies: Array.Empty<AssetDependencyDto>());

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

        public string? LastLoadedWorkspaceKey { get; private set; }

        public WorkspaceChangeSetDto? Load(string workspaceKey)
        {
            LastLoadedWorkspaceKey = workspaceKey;
            return ChangeSet;
        }

        public void Save(WorkspaceChangeSetDto changeSet)
        {
            SavedChangeSet = changeSet;
        }

        public void Delete(string workspaceKey) =>
            throw new NotSupportedException();
    }

    private sealed class TestAssetEditDraftTextStore : IAssetEditDraftTextStore
    {
        public int SaveTextPayloadCallCount { get; private set; }

        public AssetEditDraftTextPayloadDto? LastSavedPayload { get; private set; }

        public PreviewCacheTextChunkDto? LoadTextChunk(
            string workspaceKey,
            string draftId,
            string draftMode,
            int offset,
            int length) =>
            throw new NotSupportedException();

        public PreviewCacheTextLineRangeDto? LoadTextLines(
            string workspaceKey,
            string draftId,
            string draftMode,
            int startLineIndex,
            int lineCount) =>
            throw new NotSupportedException();

        public PreviewCacheTextSearchResultDto? SearchText(
            string workspaceKey,
            string draftId,
            string draftMode,
            string searchText,
            int startLineIndex,
            int maxMatchCount,
            bool matchCase) =>
            throw new NotSupportedException();

        public void SaveTextPayload(AssetEditDraftTextPayloadDto payload)
        {
            SaveTextPayloadCallCount++;
            LastSavedPayload = payload;
        }

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
