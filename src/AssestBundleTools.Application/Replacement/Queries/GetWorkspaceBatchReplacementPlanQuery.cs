using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Queries;

/// <summary>
/// 从外部导出目录生成工作区级批量替换计划。
/// </summary>
public sealed record GetWorkspaceBatchReplacementPlanQuery(
    WorkspaceId WorkspaceId,
    string? WorkspaceKey,
    string SourceRootDirectory,
    WorkspaceBatchReplacementMatchingMode MatchingMode = WorkspaceBatchReplacementMatchingMode.Auto,
    bool IncludeUnchanged = true);
