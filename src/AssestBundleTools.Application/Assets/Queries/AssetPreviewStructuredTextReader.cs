using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Assets.Queries;

internal static class AssetPreviewStructuredTextReader
{
    public static bool TryReadText(
        Result<AssetPreviewDto> previewResult,
        out string text)
    {
        text = string.Empty;
        if (!previewResult.IsSuccess ||
            previewResult.Value is not { Kind: AssetPreviewKind.Text } preview)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(preview.FullTextContent))
        {
            text = preview.FullTextContent;
            return true;
        }

        if (preview.TextDisplay?.IsTruncated == true)
        {
            return false;
        }

        text = preview.TextContent ?? string.Empty;
        return !string.IsNullOrWhiteSpace(text);
    }

    public static string ResolveText(AssetPreviewDto preview)
    {
        if (!string.IsNullOrWhiteSpace(preview.FullTextContent))
        {
            return preview.FullTextContent;
        }

        return preview.TextDisplay?.IsTruncated == true
            ? string.Empty
            : preview.TextContent ?? string.Empty;
    }

    public static bool TryCreateTextSource(
        Result<AssetPreviewDto> previewResult,
        string workspaceDataRootPath,
        out AssetPreviewTextSourceDto source)
    {
        source = null!;
        if (!previewResult.IsSuccess ||
            previewResult.Value is not { Kind: AssetPreviewKind.Text } preview)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(preview.FullTextContent))
        {
            source = AssetPreviewTextSourceDto.FromInline(preview.FullTextContent);
            return true;
        }

        if (preview.TextDisplay?.IsTruncated == true)
        {
            if (preview.FullContentReference is null ||
                string.IsNullOrWhiteSpace(workspaceDataRootPath))
            {
                return false;
            }

            source = AssetPreviewTextSourceDto.FromReference(
                preview.FullContentReference,
                workspaceDataRootPath.Trim(),
                ResolveReferenceCharacterLength(preview));
            return true;
        }

        var text = preview.TextContent ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        source = AssetPreviewTextSourceDto.FromInline(text);
        return true;
    }

    private static int? ResolveReferenceCharacterLength(AssetPreviewDto preview)
    {
        if (preview.TextDisplay?.FullCharacterCount is >= 0)
        {
            return preview.TextDisplay.FullCharacterCount;
        }

        if (preview.FullContentReference?.ContentLength is >= 0 and <= int.MaxValue)
        {
            return (int)preview.FullContentReference.ContentLength;
        }

        return null;
    }
}
