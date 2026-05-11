namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 保存撤销或恢复所需的操作前状态快照。
/// </summary>
public sealed record WorkspaceOperationStateDto(
    bool? WasModified = null,
    DateTimeOffset? LastExtractedAtUtc = null,
    WorkspaceOperationSourceDto? ReplacementSource = null,
    string? DraftId = null,
    string? CheckpointId = null);
