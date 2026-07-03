using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Saving.Dtos;

/// <summary>
/// 表示“另存为新包”执行后的摘要结果。
/// </summary>
public sealed record WorkspaceSaveExecutionResultDto(
    WorkspaceId WorkspaceId,
    string WorkspaceDisplayName,
    string OutputDirectory,
    int PlannedBundleCount,
    int TotalChangeCount,
    DateTimeOffset CompletedAtUtc,
    int ResolvedArtifactInputCount = 0,
    int UnresolvedArtifactInputCount = 0,
    int OutputReadyBundleCount = 0,
    int OutputBlockedBundleCount = 0,
    int OutputUnprobedBundleCount = 0,
    bool IsReadyForWrite = false,
    WorkspaceSaveExecutionStatusDto Status = WorkspaceSaveExecutionStatusDto.NotWritten,
    int SucceededBundleCount = 0,
    int FailedBundleCount = 0,
    int WrittenBundleCount = 0,
    string? ExportManifestPath = null,
    IReadOnlyList<WorkspaceSaveBundleExecutionResultDto>? BundleResults = null)
{
    public IReadOnlyList<WorkspaceSaveBundleExecutionResultDto> BundleResults { get; init; } =
        BundleResults ?? Array.Empty<WorkspaceSaveBundleExecutionResultDto>();
}
