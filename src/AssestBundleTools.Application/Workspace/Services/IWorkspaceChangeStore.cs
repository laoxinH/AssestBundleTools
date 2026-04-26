using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 负责读写单工作区的持久化变更暂存。
/// </summary>
public interface IWorkspaceChangeStore
{
    WorkspaceChangeSetDto? Load(string workspaceKey);

    void Save(WorkspaceChangeSetDto changeSet);

    void Delete(string workspaceKey);
}
