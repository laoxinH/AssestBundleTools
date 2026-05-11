namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示操作记录上的稳定诊断信息。
/// </summary>
public sealed record WorkspaceOperationDiagnosticDto(
    string Code,
    string Severity,
    string? MessageToken = null,
    string? Detail = null,
    string? SourcePath = null);
