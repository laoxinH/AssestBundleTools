using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 负责读写单个工作区自己的恢复元数据。
/// </summary>
public interface IWorkspaceMetadataStore
{
    WorkspaceMetadataDto? Load(string workspaceKey);

    void Save(WorkspaceMetadataDto metadata);

    void Delete(string workspaceKey);
}
