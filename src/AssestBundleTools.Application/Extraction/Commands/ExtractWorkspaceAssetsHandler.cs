using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Services;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Extraction.Commands;

/// <summary>
/// 执行工作区级批量提取骨架。
/// </summary>
/// <remarks>
/// 当前阶段执行后只回写工作区中的提取时间与建议输出路径，不执行真实文件导出。
/// </remarks>
public sealed class ExtractWorkspaceAssetsHandler : ICommandHandler<ExtractWorkspaceAssetsCommand, Result<WorkspaceBatchExtractionResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;

    public ExtractWorkspaceAssetsHandler(IWorkspaceSessionRegistry? workspaceSessionRegistry = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? InMemoryWorkspaceSessionRegistry.Shared;
    }

    public Task<Result<WorkspaceBatchExtractionResultDto>> HandleAsync(
        ExtractWorkspaceAssetsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<WorkspaceBatchExtractionResultDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return Task.FromResult(Result<WorkspaceBatchExtractionResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(command.WorkspaceId))
        {
            return Task.FromResult(Result<WorkspaceBatchExtractionResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(command.WorkspaceId)));
        }

        var plan = WorkspaceBatchExtractionPlanner.BuildPlan(
            _workspaceSessionRegistry,
            command.WorkspaceId,
            command.AssetTypeFilter,
            command.OutputDirectory);

        if (plan.Assets.Count == 0)
        {
            return Task.FromResult(Result<WorkspaceBatchExtractionResultDto>.Failure(
                ApplicationErrors.NoBatchExtractionCandidates(command.WorkspaceId)));
        }

        var affectedBundles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var asset in plan.Assets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _workspaceSessionRegistry.TryExtractAsset(
                command.WorkspaceId,
                asset.BundleId,
                asset.AssetId,
                out _);

            affectedBundles.Add(asset.BundleId.Value);
        }

        return Task.FromResult(Result<WorkspaceBatchExtractionResultDto>.Success(
            new WorkspaceBatchExtractionResultDto(
                WorkspaceId: command.WorkspaceId,
                AssetTypeFilter: command.AssetTypeFilter,
                SuggestedOutputDirectory: plan.SuggestedOutputDirectory,
                ExtractedAssetCount: plan.Assets.Count,
                ExtractedBundleCount: affectedBundles.Count,
                CompletedAtUtc: DateTimeOffset.UtcNow)));
    }
}
