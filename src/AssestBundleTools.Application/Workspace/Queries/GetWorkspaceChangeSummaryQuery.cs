using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Workspace.Queries;

/// <summary>
/// 查询当前工作区的提取、替换与待保存变更摘要。
/// </summary>
public sealed record GetWorkspaceChangeSummaryQuery(WorkspaceId WorkspaceId);
