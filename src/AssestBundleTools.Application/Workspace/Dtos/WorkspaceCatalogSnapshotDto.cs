namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示完整的工作区目录快照。
/// </summary>
/// <remarks>
/// 后续无论是最近工作区列表、固定项还是最后活动工作区，
/// 都应统一从这里恢复，而不是分散到多个零散文件中。
/// </remarks>
public sealed record WorkspaceCatalogSnapshotDto(
    int Version,
    string? LastActiveWorkspaceKey,
    IReadOnlyList<WorkspaceCatalogEntryDto> RecentWorkspaces)
{
    public static WorkspaceCatalogSnapshotDto Empty { get; } =
        new(
            Version: 1,
            LastActiveWorkspaceKey: null,
            RecentWorkspaces: Array.Empty<WorkspaceCatalogEntryDto>());
}
