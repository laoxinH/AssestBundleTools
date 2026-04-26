using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区打开后可直接供 UI 展示的摘要信息。
/// </summary>
public sealed record WorkspaceSummaryDto(
    WorkspaceId WorkspaceId,
    string DisplayName,
    string SourcePath,
    WorkspaceMode Mode,
    PaneId ActivePaneId,
    int BundleCount,
    int AssetCount,
    DateTimeOffset OpenedAtUtc);
