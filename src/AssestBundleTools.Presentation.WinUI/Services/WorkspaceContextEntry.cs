using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 表示应用级工作区上下文中登记的一项工作区摘要。
/// </summary>
/// <remarks>
/// 该类型同时兼顾“目录项摘要”和“当前会话里的活动工作区摘要”：
/// 如果 <see cref="WorkspaceSummary"/> 为空，说明它只是从目录元数据恢复出来的最近项；
/// 如果不为空，说明当前会话里已经真正打开了这个工作区。
/// </remarks>
public sealed record WorkspaceContextEntry(
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
