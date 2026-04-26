namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示资源预览链路中的结构化诊断结果。
/// </summary>
/// <remarks>
/// 该对象只承载“缓存为何命中/失效、是否退回摘要以及原因”这些稳定语义，
/// 不直接绑定 UI 文案，方便工作台、日志和后续诊断页复用。
/// </remarks>
public sealed record AssetPreviewDiagnosticDto(
    AssetPreviewCacheStatusKind CacheStatus,
    AssetPreviewFallbackReasonKind FallbackReason,
    string? Detail = null);
