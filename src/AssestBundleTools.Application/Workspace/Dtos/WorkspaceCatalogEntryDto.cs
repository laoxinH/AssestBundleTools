using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示应用级工作区目录中的一条轻量摘要记录。
/// </summary>
/// <remarks>
/// 该类型只保存“最近工作区 / 固定工作区 / 最近激活时间”这类目录状态，
/// 不承载真正的资源包、资产和变更队列数据。
/// </remarks>
public sealed record WorkspaceCatalogEntryDto(
    string WorkspaceKey,
    string DisplayName,
    string SourcePath,
    string? WorkspaceDataRootPath,
    WorkspaceMode WorkspaceMode,
    DateTimeOffset LastActivatedAtUtc,
    bool IsPinned,
    string? LastKnownState);
