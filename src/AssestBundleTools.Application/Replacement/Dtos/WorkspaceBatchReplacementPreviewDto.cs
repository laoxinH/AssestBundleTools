using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示当前工作区的批量替换计划预览。
/// </summary>
/// <remarks>
/// 当前阶段不直接在这里重做单资源替换交互，
/// 而是把已经进入工作区替换队列的资源项聚合出来，
/// 作为批处理页的任务承载与后续真实批量替换执行入口。
/// </remarks>
public sealed record WorkspaceBatchReplacementPreviewDto(
    WorkspaceId WorkspaceId,
    string WorkspaceDisplayName,
    BatchReplacementSourceKind SourceFilter,
    int CandidateAssetCount,
    int CandidateBundleCount,
    int WorkspaceReplacementCount,
    int FileReplacementCount,
    IReadOnlyList<WorkspaceBatchReplacementBundlePlanDto> BundlePlans);
