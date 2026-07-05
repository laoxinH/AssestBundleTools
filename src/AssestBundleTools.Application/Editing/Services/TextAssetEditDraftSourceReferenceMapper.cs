using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Editing.Services;

/// <summary>
/// 在预览完整内容引用与可持久化草稿源引用之间做稳定映射。
/// </summary>
internal static class TextAssetEditDraftSourceReferenceMapper
{
    public static WorkspaceAssetEditDraftSourceReferenceDto? ToWorkspaceReference(
        AssetPreviewFullContentReferenceDto? reference)
    {
        if (reference is null ||
            string.IsNullOrWhiteSpace(reference.ContentType) ||
            string.IsNullOrWhiteSpace(reference.WorkspaceKey) ||
            reference.ContentLength < 0)
        {
            return null;
        }

        return new WorkspaceAssetEditDraftSourceReferenceDto(
            reference.Kind.ToString(),
            reference.ContentType.Trim(),
            reference.ContentLength,
            reference.WorkspaceKey.Trim(),
            NormalizeOptional(reference.PreviewCacheKey),
            NormalizeOptional(reference.SourcePath),
            NormalizeOptional(reference.DraftId),
            NormalizeOptional(reference.DraftMode));
    }

    public static AssetPreviewFullContentReferenceDto? ToPreviewReference(
        WorkspaceAssetEditDraftSourceReferenceDto? reference)
    {
        if (reference is null ||
            !Enum.TryParse<AssetPreviewFullContentReferenceKind>(
                NormalizeRequired(reference.Kind),
                ignoreCase: true,
                out var kind) ||
            string.IsNullOrWhiteSpace(reference.ContentType) ||
            string.IsNullOrWhiteSpace(reference.WorkspaceKey) ||
            reference.ContentLength < 0)
        {
            return null;
        }

        return new AssetPreviewFullContentReferenceDto(
            kind,
            reference.ContentType.Trim(),
            reference.ContentLength,
            reference.WorkspaceKey.Trim(),
            NormalizeOptional(reference.PreviewCacheKey),
            NormalizeOptional(reference.SourcePath),
            NormalizeOptional(reference.DraftId),
            NormalizeOptional(reference.DraftMode));
    }

    private static string NormalizeRequired(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
