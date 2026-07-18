namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示一次工作区导入处理的阶段级耗时诊断。
/// </summary>
public sealed record WorkspaceImportPerformanceDiagnosticDto(
    long TotalDurationMs,
    long PlanningDurationMs,
    long ScreeningDurationMs,
    long ProbeDurationMs,
    long WorkerDurationMs,
    long CommitDurationMs,
    int BundleWorkerCount,
    int ProbeDecryptorCount,
    int ProbeEvaluationCount,
    int FingerprintEvaluationCount = 0,
    int RecognitionCacheHitCount = 0,
    int ProbeInputFileCount = 0);
