using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示一次“从工作区移除资源包”操作的结果摘要。
/// </summary>
public sealed record WorkspaceBundleRemovalResultDto(
    string WorkspaceKey,
    BundleId BundleId,
    string BundleSourcePath,
    bool RemovedDirectImportSource,
    bool AddedExcludedBundleSourcePath,
    int RemainingRegisteredSourceCount,
    long GateWaitDurationMs,
    long ElapsedDurationMs,
    IReadOnlyList<WorkspaceBundleRemovalCleanupIssueDto> CleanupIssues)
{
    public bool CacheCleanupCompleted => CleanupIssues.Count == 0;
}

public sealed record WorkspaceBundleRemovalCleanupIssueDto(
    WorkspaceBundleRemovalCleanupStage Stage,
    string? Target,
    string ExceptionType,
    string Message);

public enum WorkspaceBundleRemovalCleanupStage
{
    SpineGroupCatalog,
    LoadedSession,
    BundleArtifact,
    BundleIndex,
    RecognitionCache,
    PreviewCatalog,
    PreviewEntry
}
