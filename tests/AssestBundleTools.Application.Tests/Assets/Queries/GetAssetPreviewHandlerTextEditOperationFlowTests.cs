using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class GetAssetPreviewHandlerTests
{
    public static async Task HandleAsync_ShouldIgnoreStaleTextEditProjection_WhenOperationHistoryHasNoEffectiveEdit()
    {
        const string originalText = "hero.png\nsize: 256,128\n";
        const string staleEditedText = "hero.png\nsize: 999,999\n";
        const string draftId = "draft-text-1";
        const string draftMode = "text-content";
        var appliedAtUtc = new DateTimeOffset(2026, 7, 5, 9, 0, 0, TimeSpan.Zero);

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        bundleCacheEntry = bundleCacheEntry with
        {
            Assets =
            [
                bundleCacheEntry.Assets[0] with
                {
                    AssetType = AssetType.TextAsset.ToString(),
                    AssetTypeDisplayName = AssetType.TextAsset.ToString(),
                    AssetPath = "Assets/Hero/Hero.atlas",
                    AssetName = "Hero.atlas"
                }
            ]
        };
        var originalPreviewPayload = CreateTextPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            originalText,
            generatorVersion: "generator-1");
        var staleDraftPreviewPayload = CreateTextPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            staleEditedText,
            generatorVersion: "edit-draft-text-v1");
        var previewCacheStore = new TestPreviewCacheStore(originalPreviewPayload);
        var draftTextStore = new TestAssetEditDraftTextStore(staleDraftPreviewPayload);
        var changeSet = new WorkspaceChangeSetDto(
            WorkspaceKey: query.WorkspaceKey,
            UpdatedAtUtc: appliedAtUtc,
            Changes: Array.Empty<WorkspaceChangeRecordDto>(),
            OperationSequence: 3,
            Operations: Array.Empty<WorkspaceOperationRecordDto>());
        var decryptor = new TestAssetBundleDecryptor(CreatePassThroughDecryptorDescriptor());
        var generator = new TestAssetPreviewGenerator();
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(
            [
                CreateAssetDetails(
                    query,
                    name: "Hero.atlas",
                    assetType: AssetType.TextAsset,
                    textEditDraftId: draftId,
                    textEditDraftMode: draftMode,
                    textEditAppliedAtUtc: appliedAtUtc.AddSeconds(2),
                    textEditDraftRevision: 3)
            ]),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore,
                assetEditDraftTextStore: draftTextStore,
                workspaceChangeStore: new TestWorkspaceChangeStore(changeSet)),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService());

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Preview should fall back to the original projection when operation history has no effective text edit.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(originalText, preview.FullTextContent ?? preview.TextContent, "Preview must not resurrect a stale text edit projection after operation deletion.");
        TestAssert.Equal(0, draftTextStore.LoadTextChunkCallCount, "Stale draft payload should not be read when no effective operation fact remains.");
    }
}
