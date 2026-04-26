using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 封装对工作区数据目录的检查与重置能力。
/// </summary>
/// <remarks>
/// 这样 Presentation 层只关心“目录里有什么、下一步该怎么引导用户”，
/// 不需要直接感知 JSON 文件结构、缓存目录结构或文件系统清理细节。
/// </remarks>
public interface IWorkspaceDataRootService
{
    string GetDefaultRootDirectoryPath();

    WorkspaceDataRootInspectionDto Inspect(string? rootDirectoryPath);

    void ResetWorkspace(string? rootDirectoryPath, string workspaceKey);
}
