using System.Text.Json.Serialization;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区操作记录的恢复语义状态。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkspaceOperationStatus>))]
public enum WorkspaceOperationStatus
{
    Planned = 0,
    Pending,
    Applied,
    Undone,
    Failed,
    Interrupted,
    Committed,
    Invalid
}
