using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Commands;

/// <summary>
/// 请求执行当前工作区的批量替换骨架。
/// </summary>
public sealed record ExecuteWorkspaceBatchReplacementCommand(
    WorkspaceId WorkspaceId,
    BatchReplacementSourceKind SourceFilter = BatchReplacementSourceKind.All);
