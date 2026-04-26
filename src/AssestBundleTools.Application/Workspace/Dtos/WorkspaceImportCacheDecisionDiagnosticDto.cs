namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示单个资源包缓存复用或失效的结构化诊断结果。
/// </summary>
public sealed record WorkspaceImportCacheDecisionDiagnosticDto(
    string SourcePath,
    string LogicalPath,
    string BundleCacheKey,
    WorkspaceImportCacheDecisionKind Kind,
    string? Detail = null);
