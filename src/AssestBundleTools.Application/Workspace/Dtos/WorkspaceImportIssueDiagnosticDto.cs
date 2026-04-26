using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示单个导入来源在处理过程中遇到的可诊断问题。
/// </summary>
public sealed record WorkspaceImportIssueDiagnosticDto(
    string SourcePath,
    string LogicalPath,
    WorkspaceImportIssueKind Kind,
    string? DecryptorId,
    string? Detail,
    IReadOnlyList<AppError> Errors);
