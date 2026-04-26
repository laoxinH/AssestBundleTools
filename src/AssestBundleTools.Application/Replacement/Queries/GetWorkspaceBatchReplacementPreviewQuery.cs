using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Queries;

/// <summary>
/// 查询当前工作区的批量替换计划预览。
/// </summary>
public sealed record GetWorkspaceBatchReplacementPreviewQuery(
    WorkspaceId WorkspaceId,
    BatchReplacementSourceKind SourceFilter = BatchReplacementSourceKind.All);
