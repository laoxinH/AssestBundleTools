using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Application.Tests.Testing;

namespace AssestBundleTools.Application.Tests.Workspace.Commands;

internal static partial class RemoveWorkspaceBundleHandlerTests
{
    public static async Task HandleAsync_ShouldKeepLogicalRemovalSuccessful_WhenArtifactCleanupIsLocked()
    {
        const string workspaceKey = "workspace-cleanup-locked";
        const string bundleSourcePath = @"C:\Bundles\Characters\hero.ab";
        var metadataStore = new TestWorkspaceMetadataStore(CreateMinimalMetadata(workspaceKey, bundleSourcePath));
        var operationTrace = new List<string>();
        var artifactStore = new TestBundleArtifactStore(
            new IOException("The artifact is still in use by a preview session."),
            operationTrace);
        var indexStore = new TestBundleIndexCacheStore();
        var previewStore = new TestPreviewCacheStore([]);
        var invalidator = new TestBundleArtifactSessionInvalidator(operationTrace);
        var handler = new RemoveWorkspaceBundleHandler(
            new TestWorkspaceMetadataStoreFactory(metadataStore),
            new TestWorkspaceDataStoreFactory(artifactStore, indexStore, previewStore),
            invalidator);

        var result = await handler.HandleAsync(new RemoveWorkspaceBundleCommand(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: @"D:\WorkspaceData",
            BundleId: new BundleId("bundle.hero"),
            BundleSourcePath: bundleSourcePath));

        TestAssert.True(result.IsSuccess, "A locked cache artifact must not roll back logical bundle removal.");
        var removal = result.Value ?? throw new InvalidOperationException("Expected removal result.");
        TestAssert.Equal(1, metadataStore.SaveCallCount, "Logical removal metadata should be persisted once.");
        TestAssert.Equal(1, indexStore.DeleteOperations.Count, "Index cleanup should continue after artifact deletion fails.");
        var cleanupIssue = TestAssert.Single(removal.CleanupIssues);
        TestAssert.Equal(
            WorkspaceBundleRemovalCleanupStage.BundleArtifact,
            cleanupIssue.Stage,
            "The cleanup diagnostic should identify the locked artifact stage.");
        TestAssert.Equal("session-invalidate", operationTrace[0], "Loaded sessions must be invalidated before physical artifact deletion.");
        TestAssert.Equal("artifact-delete", operationTrace[1], "Artifact deletion should run after loaded-session invalidation.");
    }

    public static async Task HandleAsync_ShouldWaitForSharedWorkspaceGate_BeforeMutatingMetadata()
    {
        const string workspaceKey = "workspace-gated-removal";
        const string bundleSourcePath = @"C:\Bundles\hero.ab";
        var metadataStore = new TestWorkspaceMetadataStore(CreateMinimalMetadata(workspaceKey, bundleSourcePath));
        var executionGate = new PausedWorkspaceImportExecutionGate();
        var handler = new RemoveWorkspaceBundleHandler(
            new TestWorkspaceMetadataStoreFactory(metadataStore),
            new TestWorkspaceDataStoreFactory(
                new TestBundleArtifactStore(),
                new TestBundleIndexCacheStore(),
                new TestPreviewCacheStore([])),
            workspaceImportExecutionGate: executionGate);

        var removalTask = handler.HandleAsync(new RemoveWorkspaceBundleCommand(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: @"D:\WorkspaceData",
            BundleId: new BundleId("bundle.hero"),
            BundleSourcePath: bundleSourcePath));

        await executionGate.Entered;
        TestAssert.Equal(0, metadataStore.SaveCallCount, "Removal must not mutate metadata while another workspace operation owns the gate.");

        executionGate.Release();
        var result = await removalTask;

        TestAssert.True(result.IsSuccess, "Removal should resume after the shared workspace gate is released.");
        TestAssert.Equal(1, metadataStore.SaveCallCount, "Removal should persist metadata after entering the shared gate.");
    }

    private static WorkspaceMetadataDto CreateMinimalMetadata(string workspaceKey, string bundleSourcePath) =>
        new(
            WorkspaceKey: workspaceKey,
            DisplayName: "Removal Test",
            SourcePath: @"C:\Bundles",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            LastOpenedAtUtc: DateTimeOffset.UtcNow,
            LastBundleId: "bundle.hero",
            LastAssetId: "asset.hero",
            LastOutputDirectory: null,
            BundleSearchKeyword: string.Empty,
            AssetSearchKeyword: string.Empty,
            AssetTypeFilter: null,
            ShowModifiedOnly: false,
            ShowPreviewableOnly: false,
            OpenBundleTabs: [],
            ActiveBundleTabId: null,
            ImportedSources:
            [
                new WorkspaceImportedSourceDto(
                    Path: bundleSourcePath,
                    ImportOrigin: WorkspaceImportOriginKind.BundleFiles,
                    PathKind: WorkspaceImportPathKind.File,
                    RecordedAtUtc: DateTimeOffset.UtcNow)
            ],
            AssetBundleDecryptorId: null,
            ExcludedBundleSourcePaths: [],
            OpenAssetEditorWindows: null);

    private sealed class PausedWorkspaceImportExecutionGate : IWorkspaceImportExecutionGate
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;

        public void Release() => _release.TrySetResult();

        public async Task<TResult> ExecuteExclusiveAsync<TResult>(
            string workspaceKey,
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return await operation(cancellationToken);
        }
    }
}
