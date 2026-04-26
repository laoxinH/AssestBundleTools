using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Saving.Dtos;

/// <summary>
/// 表示“另存为新包”骨架执行后的摘要结果。
/// </summary>
public sealed record WorkspaceSaveExecutionResultDto(
    WorkspaceId WorkspaceId,
    string WorkspaceDisplayName,
    string OutputDirectory,
    int PlannedBundleCount,
    int TotalChangeCount,
    DateTimeOffset CompletedAtUtc);
