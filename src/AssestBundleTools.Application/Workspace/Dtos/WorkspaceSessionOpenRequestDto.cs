using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示一次工作区运行时会话打开请求。
/// </summary>
public sealed record WorkspaceSessionOpenRequestDto(
    string WorkspaceKey,
    string WorkspaceDataRootPath,
    string WorkspacePath,
    string WorkspaceDisplayName,
    WorkspaceMode InitialMode);
