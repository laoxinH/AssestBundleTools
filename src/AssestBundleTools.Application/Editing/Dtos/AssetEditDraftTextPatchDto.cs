using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Editing.Dtos;

/// <summary>
/// 表示一次针对编辑草稿文本载荷的范围补丁写入请求。
/// </summary>
public sealed record AssetEditDraftTextPatchDto(
    string WorkspaceKey,
    string DraftId,
    string DraftMode,
    string ContentType,
    IReadOnlyList<TextAssetEditDraftRangeEditDto> RangeEdits,
    TextAssetEditDraftPatchBaselineDto Baseline,
    DateTimeOffset UpdatedAtUtc,
    int MaxTextLength,
    string? SourceFingerprint = null,
    string? SourceDescription = null,
    string? Summary = null,
    WorkspaceAssetEditDraftSourceReferenceDto? SourceReference = null);
