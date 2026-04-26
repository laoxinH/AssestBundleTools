using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Replacement.Commands;

/// <summary>
/// 执行批量替换骨架。
/// </summary>
/// <remarks>
/// 当前阶段不在这里重放单资源替换命令，
/// 而是验证并汇总已经进入工作区替换队列的资源项，
/// 让批处理页先具备“任务执行”和“结果反馈”的稳定承载面。
/// </remarks>
public sealed class ExecuteWorkspaceBatchReplacementHandler : ICommandHandler<ExecuteWorkspaceBatchReplacementCommand, Result<WorkspaceBatchReplacementResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;

    public ExecuteWorkspaceBatchReplacementHandler(IWorkspaceSessionRegistry? workspaceSessionRegistry = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? InMemoryWorkspaceSessionRegistry.Shared;
    }

    public Task<Result<WorkspaceBatchReplacementResultDto>> HandleAsync(
        ExecuteWorkspaceBatchReplacementCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<WorkspaceBatchReplacementResultDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return Task.FromResult(Result<WorkspaceBatchReplacementResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(command.WorkspaceId))
        {
            return Task.FromResult(Result<WorkspaceBatchReplacementResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(command.WorkspaceId)));
        }

        var plan = WorkspaceBatchReplacementPlanner.BuildPlan(
            _workspaceSessionRegistry,
            command.WorkspaceId,
            command.SourceFilter);

        if (plan.Items.Count == 0)
        {
            return Task.FromResult(Result<WorkspaceBatchReplacementResultDto>.Failure(
                ApplicationErrors.NoBatchReplacementCandidates(command.WorkspaceId)));
        }

        var processedBundleCount = plan.Items
            .Select(static item => item.BundleId.Value)
            .Distinct(StringComparer.Ordinal)
            .Count();

        return Task.FromResult(Result<WorkspaceBatchReplacementResultDto>.Success(
            new WorkspaceBatchReplacementResultDto(
                WorkspaceId: plan.WorkspaceId,
                SourceFilter: plan.SourceFilter,
                ProcessedAssetCount: plan.Items.Count,
                ProcessedBundleCount: processedBundleCount,
                WorkspaceReplacementCount: plan.Items.Count(static item => item.HasWorkspaceReplacement),
                FileReplacementCount: plan.Items.Count(static item => item.HasFileReplacement),
                CompletedAtUtc: DateTimeOffset.UtcNow)));
    }
}
