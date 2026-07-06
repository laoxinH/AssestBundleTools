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
}
