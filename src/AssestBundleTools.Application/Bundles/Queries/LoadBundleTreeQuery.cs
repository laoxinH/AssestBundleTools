using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Bundles.Queries;

/// <summary>
/// 请求加载某个工作区的 Bundle 树。
/// </summary>
public sealed record LoadBundleTreeQuery(WorkspaceId WorkspaceId);
