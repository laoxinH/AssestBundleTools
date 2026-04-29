namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示当前应用会话里仍然保留的一条工作区实时摘要。
/// </summary>
public sealed record WorkspaceContextLiveSessionDto(
    string WorkspaceKey,
    string? WorkspaceDataRootPath,
    string? AssetBundleDecryptorId,
    WorkspaceSummaryDto WorkspaceSummary);
