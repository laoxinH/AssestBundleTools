using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Commands;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Services;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Tests.Saving;

internal static partial class SaveWorkspaceHandlersTests
{
    public static async Task SaveWorkspaceAsNewBundleHandler_ShouldReportBundleWriteDiagnostics()
    {
        const string workspaceKey = "workspace-save-key";
        const string workspaceDataRootPath = @"D:\WorkspaceData";

        var workspaceId = CreateWorkspaceId();
        var alphaBundleId = new BundleId("bundle-alpha");
        var zetaBundleId = new BundleId("bundle-zeta");
        var alphaIndexEntry = CreateBundleIndexEntry(workspaceKey, "bundle-cache-alpha", alphaBundleId, "Alpha.ab");
        var zetaIndexEntry = CreateBundleIndexEntry(workspaceKey, "bundle-cache-zeta", zetaBundleId, "zeta.ab");
        var artifactStore = new TestBundleArtifactStore(
        [
            CreateArtifactManifest(
                workspaceKey,
                alphaIndexEntry.BundleCacheKey,
                @"D:\WorkspaceData\cache\bundles\workspace-save-key\bundle-cache-alpha\normalized.bundle",
                alphaIndexEntry.SourceFingerprint),
            CreateArtifactManifest(
                workspaceKey,
                zetaIndexEntry.BundleCacheKey,
                @"D:\WorkspaceData\cache\bundles\workspace-save-key\bundle-cache-zeta\normalized.bundle",
                zetaIndexEntry.SourceFingerprint)
        ]);
        var registry = new TestWorkspaceSessionRegistry
        {
            WorkspaceChangeSummary = CreateWorkspaceChangeSummary(
                workspaceId,
                [
                    CreateChangeItem(
                        bundleId: alphaBundleId,
                        assetId: new AssetId("asset-1"),
                        bundleDisplayName: "Alpha.ab",
                        assetName: "ConfigText",
                        isModified: true),
                    CreateChangeItem(
                        bundleId: zetaBundleId,
                        assetId: new AssetId("asset-2"),
                        bundleDisplayName: "zeta.ab",
                        assetName: "EnemyPortrait",
                        isModified: true)
                ])
        };
        var exporter = new SynchronousCoordinatedBundleExporter(expectedStartedCount: 2);
        var diagnostics = new RecordingProgress<WorkspaceSaveExecutionDiagnosticDto>();
        var handler = new SaveWorkspaceAsNewBundleHandler(
            registry,
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([alphaIndexEntry, zetaIndexEntry]),
                artifactStore),
            new ReadyWorkspaceSaveOutputProbe(),
            exporter,
            new RecordingBundleExportManifestStore());

        var command = new SaveWorkspaceAsNewBundleCommand(
            workspaceId,
            OutputDirectory: @"D:\Exports",
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath,
            BundleWriteConcurrency: 2,
            Diagnostics: diagnostics);
        var executionTask = Task.Run(async () => await handler.HandleAsync(command));
        var bothWritersStarted = await Task.WhenAny(
            exporter.StartedThresholdTask,
            Task.Delay(TimeSpan.FromSeconds(5))) == exporter.StartedThresholdTask;
        exporter.Release();

        var result = await executionTask;

        TestAssert.True(result.IsSuccess, "Export should complete after the coordinated exporter is released.");
        TestAssert.True(bothWritersStarted, "Both bundle writers should be able to enter the exporter before the first one completes.");
        var diagnosticSnapshot = diagnostics.Snapshot();
        TestAssert.True(
            diagnosticSnapshot.Any(static diagnostic =>
                diagnostic.Kind == WorkspaceSaveExecutionDiagnosticKindDto.BundleWritePlan &&
                diagnostic.BundleWriteConcurrency == 2 &&
                diagnostic.TotalBundleCount == 2),
            "Diagnostics should report the selected bundle count and resolved bundle writer concurrency.");
        TestAssert.True(
            diagnosticSnapshot.Any(static diagnostic =>
                diagnostic.Kind == WorkspaceSaveExecutionDiagnosticKindDto.BundleWriteStarted &&
                diagnostic.ActiveBundleWriteCount == 2),
            "Diagnostics should expose the active writer count so parallel bundle writes are observable.");
        TestAssert.Equal(
            2,
            diagnosticSnapshot.Count(static diagnostic =>
                diagnostic.Kind == WorkspaceSaveExecutionDiagnosticKindDto.BundleWriteFinished &&
                diagnostic.Status == WorkspaceSaveBundleExecutionStatusDto.Succeeded.ToString()),
            "Diagnostics should report one finished event for each written bundle.");
    }

    private sealed class SynchronousCoordinatedBundleExporter : IWorkspaceBundleExporter
    {
        private readonly ManualResetEventSlim _release = new();
        private readonly TaskCompletionSource _startedThreshold = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly int _expectedStartedCount;
        private int _startedCount;

        public SynchronousCoordinatedBundleExporter(int expectedStartedCount)
        {
            _expectedStartedCount = expectedStartedCount;
        }

        public Task StartedThresholdTask => _startedThreshold.Task;

        public void Release() => _release.Set();

        public Task<Result<WorkspaceBundleExportResultDto>> ExportAsync(
            WorkspaceBundleExportRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _startedCount) >= _expectedStartedCount)
            {
                _startedThreshold.TrySetResult();
            }

            _release.Wait(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result<WorkspaceBundleExportResultDto>.Success(CreateSuccessfulExportResult(request)));
        }
    }

    private sealed class RecordingProgress<T> : IProgress<T>
    {
        private readonly object _gate = new();
        private readonly List<T> _values = [];

        public void Report(T value)
        {
            lock (_gate)
            {
                _values.Add(value);
            }
        }

        public IReadOnlyList<T> Snapshot()
        {
            lock (_gate)
            {
                return _values.ToArray();
            }
        }
    }
}
