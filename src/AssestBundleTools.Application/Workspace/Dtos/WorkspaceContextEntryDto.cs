using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区上下文目录中的一个已解析条目。
/// </summary>
/// <remarks>
/// 该类型同时承载：
/// 1. 最近工作区目录中的恢复摘要；
/// 2. 当前应用会话里仍然保留的实时工作区摘要。
/// </remarks>
public sealed record WorkspaceContextEntryDto(
    string WorkspaceKey,
    string DisplayName,
    string SourcePath,
    string WorkspaceDataRootPath,
    string? AssetBundleDecryptorId,
    WorkspaceMode WorkspaceMode,
    DateTimeOffset LastActivatedAtUtc,
    bool IsPinned,
    WorkspaceAvailabilityStatus AvailabilityStatus,
    string? LastKnownState,
    WorkspaceSummaryDto? WorkspaceSummary)
{
    public bool HasLiveSession => WorkspaceSummary is not null;

    public bool IsAvailable => AvailabilityStatus == WorkspaceAvailabilityStatus.Available;
}
