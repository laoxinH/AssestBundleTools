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
            return Array.Empty<WorkspaceImportBundleWorkResult>();
        }

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
        var tasks = Enumerable.Range(0, workerCount)
            .Select(_ => ExecuteWorkerLoopAsync(plan, candidateFiles, results, sharedIndex, cancellationToken))
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
        }
    }

    private sealed class WorkerIndexBox
    {
        public int NextIndex;
    }
}
