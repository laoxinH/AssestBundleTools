using System.Text.Json.Serialization;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示恢复计划中的稳定问题分类。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkspaceOperationRecoveryIssueKind>))]
public enum WorkspaceOperationRecoveryIssueKind
{
    None = 0,
    InterruptedPendingOperation,
    FailedOperation,
    InvalidOperation,
    SourceMissing,
    SourceMismatch,
    DraftMissing
}
