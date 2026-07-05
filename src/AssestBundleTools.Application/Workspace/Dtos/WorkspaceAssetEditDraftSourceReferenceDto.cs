namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示可恢复文本草稿创建时引用的源文本 payload，不承载真实正文。
/// </summary>
public sealed record WorkspaceAssetEditDraftSourceReferenceDto(
    string Kind,
    string ContentType,
    long ContentLength,
    string WorkspaceKey,
    string? PreviewCacheKey = null,
    string? SourcePath = null,
    string? DraftId = null,
    string? DraftMode = null);
