using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Saving.Queries;

/// <summary>
/// 查询当前工作区的保存前预览摘要。
/// </summary>
public sealed record GetWorkspaceSavePreviewQuery(
    WorkspaceId WorkspaceId,
    string? OutputDirectory = null,
    string? WorkspaceKey = null,
    string? WorkspaceDataRootPath = null);
