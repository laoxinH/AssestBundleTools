using System.Text.Json.Serialization;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示恢复计划建议的稳定动作。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkspaceOperationRecoveryActionKind>))]
public enum WorkspaceOperationRecoveryActionKind
{
    None = 0,
    Retry,
    RelinkSource,
    MarkFailed,
    Undo,
    IgnoreForNow,
    DiscardRecord
}
