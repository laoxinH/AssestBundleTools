using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示可被结构化解析或运行时适配器读取的完整文本来源。
/// </summary>
public sealed record AssetPreviewTextSourceDto(
    string? InlineText,
    AssetPreviewFullContentReferenceDto? Reference,
    string WorkspaceDataRootPath,
    int? ContentLength)
{
    public static AssetPreviewTextSourceDto FromInline(string text) =>
        new(text, Reference: null, WorkspaceDataRootPath: string.Empty, ContentLength: text.Length);

    public static AssetPreviewTextSourceDto FromReference(
        AssetPreviewFullContentReferenceDto reference,
        string workspaceDataRootPath,
        int? contentLength = null) =>
        new(null, reference, workspaceDataRootPath, contentLength);
}

public sealed record AssetPreviewTextSourceReadResult(
    bool IsCancelled,
    string? Text,
    AppError[] Errors)
{
    public static AssetPreviewTextSourceReadResult Cancelled { get; } =
        new(true, null, Array.Empty<AppError>());

    public static AssetPreviewTextSourceReadResult Success(string text) =>
        new(false, text, Array.Empty<AppError>());

    public static AssetPreviewTextSourceReadResult Failure(IReadOnlyList<AppError> errors) =>
        new(false, null, errors.ToArray());
}
