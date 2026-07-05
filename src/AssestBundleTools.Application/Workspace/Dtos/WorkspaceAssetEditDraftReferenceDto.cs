namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示资产文档持有的轻量编辑草稿引用。
/// </summary>
/// <remarks>
/// 该 DTO 只保存恢复和冲突提示所需的草稿标识，不保存真实草稿内容。
/// 所属工作区、资源包与资产身份由外层工作区元数据和资产标签恢复状态提供。
/// </remarks>
public sealed record WorkspaceAssetEditDraftReferenceDto(
    string DraftId,
    string DraftMode,
    string? SourceFingerprint = null,
    string? SourceDescription = null,
    bool IsAppliedBaseline = false,
    int? DraftRevision = null);

