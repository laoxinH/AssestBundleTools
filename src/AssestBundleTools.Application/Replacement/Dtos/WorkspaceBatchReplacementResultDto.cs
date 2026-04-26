using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示批量替换骨架执行后的摘要结果。
/// </summary>
public sealed record WorkspaceBatchReplacementResultDto(
    WorkspaceId WorkspaceId,
    BatchReplacementSourceKind SourceFilter,
    int ProcessedAssetCount,
    int ProcessedBundleCount,
    int WorkspaceReplacementCount,
    int FileReplacementCount,
    DateTimeOffset CompletedAtUtc);
