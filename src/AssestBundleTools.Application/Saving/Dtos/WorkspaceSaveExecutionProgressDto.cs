namespace AssestBundleTools.Application.Saving.Dtos;

public enum WorkspaceSaveExecutionStageDto
{
    Planning,
    ResourceProcessing,
    BundleWriting,
    ManifestWriting,
    Completed
}

/// <summary>
/// 表示保存导出后台任务的聚合进度。
/// </summary>
public sealed record WorkspaceSaveExecutionProgressDto(
    WorkspaceSaveExecutionStageDto Stage,
    int CompletedBundleCount,
    int TotalBundleCount,
    string? CurrentBundleDisplayName = null,
    int SucceededBundleCount = 0,
    int FailedBundleCount = 0);
