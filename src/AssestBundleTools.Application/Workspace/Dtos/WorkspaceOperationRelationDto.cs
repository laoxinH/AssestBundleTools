namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 描述一条操作记录与其他资源或操作之间的可恢复关联。
/// </summary>
public sealed record WorkspaceOperationRelationDto(
    string RelationId,
    string RelationKind,
    string Role,
    WorkspaceOperationTargetDto? RelatedTarget = null,
    string? RelatedOperationId = null);
