using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 请求将某个资源包从当前工作区中移除，但不删除原始文件。
/// </summary>
public sealed record RemoveWorkspaceBundleCommand(
    string WorkspaceKey,
    string WorkspaceDataRootPath,
    BundleId BundleId,
    string BundleSourcePath);
