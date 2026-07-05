namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区变更暂存层中的编辑草稿描述元数据。
/// </summary>
/// <remarks>
/// 该 DTO 只描述草稿身份、来源和生命周期，不承载真实草稿内容。
/// </remarks>
public sealed record WorkspaceAssetEditDraftDescriptorDto(
    string DraftId,
    string DraftMode,
    string? SourceFingerprint,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ExpiresAtUtc = null,
    string? SourceDescription = null,
    string? Summary = null,
    WorkspaceAssetEditDraftSourceReferenceDto? SourceReference = null,
    bool? IsPayloadMaterialized = null);
