using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Services;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Saving.Commands;

/// <summary>
/// 执行“另存为新包”骨架。
/// </summary>
/// <remarks>
/// 当前阶段仍不写入真实文件，执行后只返回一份稳定的执行摘要，
/// 供批处理页承载“已验证保存计划”的状态与日志。
/// </remarks>
public sealed class SaveWorkspaceAsNewBundleHandler : ICommandHandler<SaveWorkspaceAsNewBundleCommand, Result<WorkspaceSaveExecutionResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;

    public SaveWorkspaceAsNewBundleHandler(IWorkspaceSessionRegistry? workspaceSessionRegistry = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? InMemoryWorkspaceSessionRegistry.Shared;
    }

    public Task<Result<WorkspaceSaveExecutionResultDto>> HandleAsync(
        SaveWorkspaceAsNewBundleCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<WorkspaceSaveExecutionResultDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return Task.FromResult(Result<WorkspaceSaveExecutionResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(command.WorkspaceId))
        {
            return Task.FromResult(Result<WorkspaceSaveExecutionResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(command.WorkspaceId)));
        }

        var plan = WorkspaceSavePlanner.BuildPlan(_workspaceSessionRegistry, command.WorkspaceId, command.OutputDirectory);
        if (plan.BundlePlans.Count == 0)
        {
            return Task.FromResult(Result<WorkspaceSaveExecutionResultDto>.Failure(ApplicationErrors.NoSaveCandidates(command.WorkspaceId)));
        }

        return Task.FromResult(Result<WorkspaceSaveExecutionResultDto>.Success(
            new WorkspaceSaveExecutionResultDto(
                WorkspaceId: plan.WorkspaceId,
                WorkspaceDisplayName: plan.WorkspaceDisplayName,
                OutputDirectory: plan.OutputDirectory,
                PlannedBundleCount: plan.BundlePlans.Count,
                TotalChangeCount: plan.TotalChangeCount,
                CompletedAtUtc: DateTimeOffset.UtcNow)));
    }
}
