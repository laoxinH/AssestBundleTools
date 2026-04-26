namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区导入时“自动识别解密器”链路的诊断摘要。
/// </summary>
public sealed record WorkspaceImportAutoDetectionDiagnosticDto(
    int DiscoveredFileCount,
    string SelectedDecryptorId,
    bool UsedFallbackDecryptor,
    IReadOnlyList<WorkspaceImportDecryptorProbeDiagnosticDto> ProbeDiagnostics);
