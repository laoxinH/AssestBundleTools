using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 描述替换操作写入可恢复操作记录时需要保留的批次与关联上下文。
/// </summary>
public sealed record ReplacementOperationContextDto(
    string? BatchId = null,
    IReadOnlyList<WorkspaceOperationRelationDto>? Relations = null);
