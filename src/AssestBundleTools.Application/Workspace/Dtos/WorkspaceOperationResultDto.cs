namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 描述一次可恢复操作完成后的结构化结果。
/// </summary>
public sealed record WorkspaceOperationResultDto(
    string? OutputPath = null,
    string? OutputDirectory = null,
    bool? OutputFileExists = null,
    string? ReplacementSourceKind = null,
    string? DraftId = null,
    string? CheckpointId = null,
    IReadOnlyList<string>? AffectedBundleIds = null,
    IReadOnlyList<string>? AffectedAssetIds = null,
    int? CompletedCount = null,
    int? FailedCount = null,
    WorkspaceSpriteEditGeometryDto? SpriteEditGeometry = null,
    int? DraftRevision = null);
