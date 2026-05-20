using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示由工作区操作记录物化出的当前有效替换。
/// </summary>
public sealed record WorkspaceEffectiveReplacementDto(
    string OperationId,
    long Sequence,
    WorkspaceOperationTargetDto Target,
    WorkspaceOperationSourceDto Source,
    DateTimeOffset? AppliedAtUtc);
