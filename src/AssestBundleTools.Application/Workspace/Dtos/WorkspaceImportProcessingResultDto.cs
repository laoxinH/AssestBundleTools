namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示一次工作区导入处理后的摘要结果。
/// </summary>
public sealed record WorkspaceImportProcessingResultDto(
    string WorkspaceKey,
    string WorkspaceDisplayName,
    string ResolvedDecryptorId,
    bool UsedAutoDetection,
    WorkspaceImportAutoDetectionDiagnosticDto? AutoDetectionDiagnostic,
    int RegisteredSourceCount,
    int DiscoveredFileCount,
    int ScreenedOutFileCount,
    int IndexedBundleCount,
    int ReusedBundleCount,
    int SkippedFileCount,
    int FailedFileCount,
    IReadOnlyList<WorkspaceImportCacheDecisionDiagnosticDto> CacheDiagnostics,
    IReadOnlyList<WorkspaceImportIssueDiagnosticDto> IssueDiagnostics,
    DateTimeOffset ProcessedAtUtc);
