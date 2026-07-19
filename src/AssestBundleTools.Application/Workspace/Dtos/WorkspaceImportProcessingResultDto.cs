namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示一次工作区导入处理后的摘要结果。
/// </summary>
/// <remarks>
/// <see cref="ScreenedOutFileCount"/> 包含当前刷新复用的识别缓存结论；
/// <see cref="NewlyScreenedOutFileCount"/> 只统计本次重新识别后新增的筛除。
/// <see cref="SkippedFileCount"/> 包含缓存复用的 Reader 拒绝；
/// <see cref="NewlySkippedFileCount"/> 只统计本次执行产生的读取跳过。
/// </remarks>
public sealed record WorkspaceImportProcessingResultDto(
    string WorkspaceKey,
    string WorkspaceDisplayName,
    string ResolvedDecryptorId,
    bool UsedAutoDetection,
    WorkspaceImportAutoDetectionDiagnosticDto? AutoDetectionDiagnostic,
    int RegisteredSourceCount,
    int DiscoveredFileCount,
    int ScreenedOutFileCount,
    int NewlyScreenedOutFileCount,
    int IndexedBundleCount,
    int ReusedBundleCount,
    int SkippedFileCount,
    int NewlySkippedFileCount,
    int FailedFileCount,
    IReadOnlyList<WorkspaceImportCacheDecisionDiagnosticDto> CacheDiagnostics,
    IReadOnlyList<WorkspaceImportIssueDiagnosticDto> IssueDiagnostics,
    DateTimeOffset ProcessedAtUtc,
    WorkspaceImportPerformanceDiagnosticDto? PerformanceDiagnostic = null);
