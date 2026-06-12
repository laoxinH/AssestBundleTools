using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Application.Replacement.Dtos;

namespace AssestBundleTools.Application.Replacement.Commands;

/// <summary>
/// 请求执行当前工作区的批量替换。
/// </summary>
public sealed record ExecuteWorkspaceBatchReplacementCommand(
    WorkspaceId WorkspaceId,
    string? WorkspaceKey,
    string? WorkspaceDataRootPath,
    string SourceRootDirectory,
    WorkspaceBatchReplacementMatchingMode MatchingMode,
    IReadOnlyList<WorkspaceBatchReplacementCandidateDto> Candidates,
    IProgress<WorkspaceBatchReplacementProgressDto>? Progress = null);
