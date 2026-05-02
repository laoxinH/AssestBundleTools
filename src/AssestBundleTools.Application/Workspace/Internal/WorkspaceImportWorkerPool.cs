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
        var candidateFiles = plan.DecryptorResolution.CandidateFiles;
        if (candidateFiles.Count == 0)
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
            TotalBundleCount: candidateFiles.Count,
            CurrentBundleLogicalPath: null));

        if (plan.ExecutionOptions.BundleWorkerCount <= 1)
        {
            return await ExecuteSerialAsync(plan, candidateFiles, cancellationToken);
        }

        return await ExecuteParallelAsync(plan, candidateFiles, cancellationToken);
    }

    private async Task<IReadOnlyList<WorkspaceImportBundleWorkResult>> ExecuteSerialAsync(
        WorkspaceImportProcessingPlan plan,
        IReadOnlyList<DiscoveredWorkspaceImportFileDto> candidateFiles,
        CancellationToken cancellationToken)
    {
        var results = new List<WorkspaceImportBundleWorkResult>(candidateFiles.Count);
        foreach (var candidateFile in candidateFiles)
        {
            results.Add(await _workspaceImportBundleWorker.ExecuteAsync(plan, candidateFile, cancellationToken));
            ReportBundleCompleted(plan, results.Count, candidateFiles.Count, candidateFile);
        }

        return results;
    }

    private async Task<IReadOnlyList<WorkspaceImportBundleWorkResult>> ExecuteParallelAsync(
        WorkspaceImportProcessingPlan plan,
        IReadOnlyList<DiscoveredWorkspaceImportFileDto> candidateFiles,
        CancellationToken cancellationToken)
    {
        var workerCount = Math.Min(plan.ExecutionOptions.BundleWorkerCount, candidateFiles.Count);
        var results = new WorkspaceImportBundleWorkResult?[candidateFiles.Count];
        var sharedIndex = new WorkerIndexBox { NextIndex = -1 };
        var progressState = new WorkerProgressBox();
        var tasks = Enumerable.Range(0, workerCount)
            .Select(_ => Task.Run(
                () => ExecuteWorkerLoopAsync(plan, candidateFiles, results, sharedIndex, progressState, cancellationToken),
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
        IReadOnlyList<DiscoveredWorkspaceImportFileDto> candidateFiles,
        WorkspaceImportBundleWorkResult?[] results,
        WorkerIndexBox nextIndexBox,
        WorkerProgressBox progressState,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidateIndex = Interlocked.Increment(ref nextIndexBox.NextIndex);
            if (candidateIndex >= candidateFiles.Count)
            {
                return;
            }

            results[candidateIndex] = await _workspaceImportBundleWorker.ExecuteAsync(
                plan,
                candidateFiles[candidateIndex],
                cancellationToken);
            var completedCount = Interlocked.Increment(ref progressState.CompletedCount);
            ReportBundleCompleted(plan, completedCount, candidateFiles.Count, candidateFiles[candidateIndex]);
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
