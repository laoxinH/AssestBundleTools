using System.IO;
using AssestBundleTools.Application.Saving.Commands;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Tests.Saving;

internal static partial class SaveWorkspaceHandlersTests
{
    public static async Task SaveWorkspaceAsNewBundleHandler_ShouldForwardAudioProcessorOutput_ToObjectWriteItem()
    {
        const string workspaceKey = "workspace-save-key";
        const string workspaceDataRootPath = @"D:\WorkspaceData";

        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-alpha");
        var assetId = new AssetId("asset-audio");
        var indexEntry = CreateBundleIndexEntry(workspaceKey, "bundle-cache-alpha", bundleId, "Alpha.ab");
        var artifactStore = new TestBundleArtifactStore(
        [
            CreateArtifactManifest(
                workspaceKey,
                indexEntry.BundleCacheKey,
                @"D:\WorkspaceData\cache\bundles\workspace-save-key\bundle-cache-alpha\normalized.bundle",
                indexEntry.SourceFingerprint)
        ]);
        var registry = new TestWorkspaceSessionRegistry
        {
            WorkspaceChangeSummary = CreateWorkspaceChangeSummary(
                workspaceId,
                [
                    CreateChangeItem(
                        bundleId: bundleId,
                        assetId: assetId,
                        bundleDisplayName: "Alpha.ab",
                        assetName: "HeroVoice",
                        isModified: true,
                        fileReplacementSourceFileName: "voice.wav",
                        assetType: AssetType.AudioClip)
                ])
        };
        registry.AddAssetDetails(CreateAssetDetails(workspaceId, bundleId, assetId, "HeroVoice", AssetType.AudioClip, "CAB-main", 84));
        var changeSet = WorkspaceChangeSetDto.Empty(workspaceKey) with
        {
            Operations =
            [
                new WorkspaceOperationRecordDto(
                    OperationId: "replace-audio-1",
                    Sequence: 1,
                    OperationKind: WorkspaceOperationKind.ReplaceFromFile,
                    Status: WorkspaceOperationStatus.Applied,
                    WorkspaceKey: workspaceKey,
                    Target: new WorkspaceOperationTargetDto(
                        BundleId: bundleId.Value,
                        AssetId: assetId.Value,
                        AssetName: "HeroVoice",
                        AssetType: AssetType.AudioClip.ToString(),
                        AssetTypeDisplayName: "AudioClip",
                        SerializedFileName: "CAB-main",
                        PathId: 84),
                    Source: new WorkspaceOperationSourceDto(
                        SourceKind: "file",
                        SourceFilePath: @"C:\Imports\voice.wav",
                        SourceFileName: "voice.wav",
                        SourceAssetType: AssetType.AudioClip.ToString()),
                    PreviousState: null,
                    Result: new WorkspaceOperationResultDto(ReplacementSourceKind: "file"),
                    CreatedAtUtc: new DateTimeOffset(2026, 7, 3, 8, 0, 0, TimeSpan.Zero),
                    AppliedAtUtc: new DateTimeOffset(2026, 7, 3, 8, 0, 0, TimeSpan.Zero),
                    CompletedAtUtc: new DateTimeOffset(2026, 7, 3, 8, 0, 0, TimeSpan.Zero))
            ]
        };
        var exporter = new RecordingBundleExporter();
        var processorExecutor = new RecordingResourceProcessorExecutor();
        var handler = new SaveWorkspaceAsNewBundleHandler(
            registry,
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([indexEntry]),
                artifactStore,
                new TestWorkspaceChangeStore(changeSet)),
            new ReadyWorkspaceSaveOutputProbe(),
            exporter,
            new RecordingBundleExportManifestStore(),
            new TestResourceProcessorCatalogStore(
            [
                CreateResourceProcessor("resource.processor.audio.fmod-fsb5", "Audio")
            ]),
            processorExecutor);

        var result = await handler.HandleAsync(new SaveWorkspaceAsNewBundleCommand(
            workspaceId,
            OutputDirectory: @"D:\Exports",
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath,
            ProcessorSelections: [new WorkspaceExportProcessorSelectionDto("Audio", "resource.processor.audio.fmod-fsb5")]));

        TestAssert.True(result.IsSuccess, "Audio file replacement save should succeed when resource processor output is available.");
        var workItem = TestAssert.Single(processorExecutor.WorkItems);
        TestAssert.Equal(AssetType.AudioClip, workItem.AssetType, "AudioClip replacement should create an audio resource processor work item.");
        TestAssert.Equal("audio", workItem.ResourceKind, "AudioClip replacement should use the audio processor slot.");
        var writeItem = TestAssert.Single(TestAssert.Single(exporter.Requests).AssetWriteItems);
        TestAssert.Equal(AssetType.AudioClip, writeItem.AssetType, "Audio write item should preserve target asset type.");
        TestAssert.Equal(workItem.InputPayloadPath, writeItem.SourceFilePath, "Audio write item should keep the original replacement file path for diagnostics.");
        TestAssert.Equal("resource.processor.audio.fmod-fsb5", writeItem.ProcessorId, "Audio write item should keep the processor id.");
        TestAssert.Equal("audio", writeItem.ResourceKind, "Audio write item should keep the processor resource kind.");
        TestAssert.Equal("output", Path.GetFileName(writeItem.ProcessorOutputPath), "Audio write item should consume processor output during bundle writing.");
    }
}
