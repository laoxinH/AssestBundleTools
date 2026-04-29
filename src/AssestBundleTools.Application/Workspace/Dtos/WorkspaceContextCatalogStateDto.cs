namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区目录快照恢复后的上下文状态。
/// </summary>
public sealed record WorkspaceContextCatalogStateDto(
    string? LastActiveWorkspaceKey,
    IReadOnlyList<WorkspaceContextEntryDto> WorkspaceEntries);
