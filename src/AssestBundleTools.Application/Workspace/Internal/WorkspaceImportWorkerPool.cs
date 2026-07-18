using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Internal;

internal sealed class WorkspaceImportWorkerPool
{
    private readonly WorkspaceImportBundleWorker _workspaceImportBundleWorker;

    public WorkspaceImportWorkerPool(WorkspaceImportBundleWorker workspaceImportBundleWorker)
    {
        _workspaceImportBundleWorker = workspaceImportBundleWorker ?? throw new ArgumentNullException(nameof(workspaceImportBundleWorker));
    }

    public async Task<IReadOnlyList<WorkspaceImportBundleWorkResult>> ExecuteAsync(
        WorkspaceImportProcessingPlan plan,
        CancellationToken cancellationToken)
    {
        var bundleCandidates = plan.DecryptorResolution.BundleCandidates;
        if (bundleCandidates.Count == 0)
        {
            plan.Command.Progress?.Report(new WorkspaceImportProgressDto(
                WorkspaceImportProgressStage.ProcessingBundles,
                CompletedBundleCount: 0,
                TotalBundleCount: 0,
                CurrentBundleLogicalPath: null));
            return Array.Empty<WorkspaceImportBundleWorkResult>();
        }

        plan.Command.Progress?.Report(new WorkspaceImportProgressDto(
            WorkspaceImportProgressStage.ProcessingBundles,
            CompletedBundleCount: 0,
            TotalBundleCount: bundleCandidates.Count,
            CurrentBundleLogicalPath: null));

        if (plan.ExecutionOptions.BundleWorkerCount <= 1)
        {
            return await ExecuteSerialAsync(plan, bundleCandidates, cancellationToken);
        }

        return await ExecuteParallelAsync(plan, bundleCandidates, cancellationToken);
    }

    private async Task<IReadOnlyList<WorkspaceImportBundleWorkResult>> ExecuteSerialAsync(
        WorkspaceImportProcessingPlan plan,
        IReadOnlyList<WorkspaceImportBundleCandidate> bundleCandidates,
        CancellationToken cancellationToken)
    {
        var results = new List<WorkspaceImportBundleWorkResult>(bundleCandidates.Count);
        foreach (var candidate in bundleCandidates)
        {
            results.Add(await _workspaceImportBundleWorker.ExecuteAsync(
                plan,
                candidate,
                cancellationToken));
            ReportBundleCompleted(plan, results.Count, bundleCandidates.Count, candidate.File);
        }

        return results;
    }

    private async Task<IReadOnlyList<WorkspaceImportBundleWorkResult>> ExecuteParallelAsync(
        WorkspaceImportProcessingPlan plan,
        IReadOnlyList<WorkspaceImportBundleCandidate> bundleCandidates,
        CancellationToken cancellationToken)
    {
        var workerCount = Math.Min(plan.ExecutionOptions.BundleWorkerCount, bundleCandidates.Count);
        var results = new WorkspaceImportBundleWorkResult?[bundleCandidates.Count];
        var sharedIndex = new WorkerIndexBox { NextIndex = -1 };
        var progressState = new WorkerProgressBox();
        var tasks = Enumerable.Range(0, workerCount)
            .Select(_ => Task.Run(
                () => ExecuteWorkerLoopAsync(plan, bundleCandidates, results, sharedIndex, progressState, cancellationToken),
                cancellationToken))
            .ToArray();
        await Task.WhenAll(tasks);

        return results
            .Select(static result => result ?? throw new InvalidOperationException("Import worker result was not populated."))
            .OrderBy(static result => result.LogicalPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static result => result.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task ExecuteWorkerLoopAsync(
        WorkspaceImportProcessingPlan plan,
        IReadOnlyList<WorkspaceImportBundleCandidate> bundleCandidates,
        WorkspaceImportBundleWorkResult?[] results,
        WorkerIndexBox nextIndexBox,
        WorkerProgressBox progressState,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidateIndex = Interlocked.Increment(ref nextIndexBox.NextIndex);
            if (candidateIndex >= bundleCandidates.Count)
            {
                return;
            }

            var candidate = bundleCandidates[candidateIndex];
            results[candidateIndex] = await _workspaceImportBundleWorker.ExecuteAsync(
                plan,
                candidate,
                cancellationToken);
            var completedCount = Interlocked.Increment(ref progressState.CompletedCount);
            ReportBundleCompleted(plan, completedCount, bundleCandidates.Count, candidate.File);
        }
    }

    private static void ReportBundleCompleted(
        WorkspaceImportProcessingPlan plan,
        int completedCount,
        int totalCount,
        DiscoveredWorkspaceImportFileDto candidateFile)
    {
        plan.Command.Progress?.Report(new WorkspaceImportProgressDto(
            WorkspaceImportProgressStage.ProcessingBundles,
            completedCount,
            totalCount,
            candidateFile.LogicalPath));
    }

    private sealed class WorkerIndexBox
    {
        public int NextIndex;
    }

    private sealed class WorkerProgressBox
    {
        public int CompletedCount;
    }
}
