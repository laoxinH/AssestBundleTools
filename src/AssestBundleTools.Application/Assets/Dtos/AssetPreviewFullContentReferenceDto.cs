namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示可通过 Application 查询继续读取的完整预览内容引用。
/// </summary>
public sealed record AssetPreviewFullContentReferenceDto(
    AssetPreviewFullContentReferenceKind Kind,
    string ContentType,
    long ContentLength,
    string WorkspaceKey,
    string? PreviewCacheKey = null,
    string? SourcePath = null);
