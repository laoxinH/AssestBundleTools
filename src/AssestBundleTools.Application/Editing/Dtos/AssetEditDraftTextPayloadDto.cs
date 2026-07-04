namespace AssestBundleTools.Application.Editing.Dtos;

/// <summary>
/// 表示编辑草稿的完整文本载荷。
/// </summary>
public sealed record AssetEditDraftTextPayloadDto(
    string WorkspaceKey,
    string DraftId,
    string DraftMode,
    string ContentType,
    string TextContent,
    DateTimeOffset UpdatedAtUtc,
    string? SourceFingerprint = null,
    string? SourceDescription = null,
    string? Summary = null);
