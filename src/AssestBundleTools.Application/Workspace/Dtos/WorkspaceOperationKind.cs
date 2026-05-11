using System.Text.Json.Serialization;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区可恢复操作记录的稳定种类。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkspaceOperationKind>))]
public enum WorkspaceOperationKind
{
    Unknown = 0,
    ExtractAsset,
    ExtractBatch,
    ReplaceFromWorkspace,
    ReplaceFromFile,
    UndoReplacement,
    EditDraftCreated,
    EditDraftUpdated,
    EditDraftApplied,
    EditDraftDiscarded,
    SavePreviewAccepted,
    SaveExecutionStarted,
    SaveExecutionCompleted,
    SaveExecutionFailed
}
