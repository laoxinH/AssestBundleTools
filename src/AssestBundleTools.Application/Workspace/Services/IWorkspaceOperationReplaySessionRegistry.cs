using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 允许在操作记录被整理后，把当前运行时会话重新同步到持久化事实流。
/// </summary>
public interface IWorkspaceOperationReplaySessionRegistry
{
    bool TryReplayWorkspaceOperations(
        WorkspaceId workspaceId,
        WorkspaceChangeSetDto? changeSet);
}
