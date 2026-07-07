namespace AssestBundleTools.Application.Saving.Dtos;

public enum WorkspaceSaveExecutionDiagnosticKindDto
{
    BundleWritePlan,
    BundleWriteStarted,
    BundleWriteFinished,
    BundleWriteSkipped
}

/// <summary>
/// 表示保存导出执行过程中的技术诊断事件。
/// </summary>
public sealed record WorkspaceSaveExecutionDiagnosticDto(
    WorkspaceSaveExecutionDiagnosticKindDto Kind,
    WorkspaceSaveExecutionStageDto Stage,
    int TotalBundleCount,
    int? BundleWriteConcurrency = null,
    int? ActiveBundleWriteCount = null,
    int? CompletedBundleCount = null,
    string? BundleId = null,
    string? BundleDisplayName = null,
    string? OutputFilePath = null,
    int? ManagedThreadId = null,
    long? ElapsedMilliseconds = null,
    string? Status = null,
    string? DiagnosticCode = null);
