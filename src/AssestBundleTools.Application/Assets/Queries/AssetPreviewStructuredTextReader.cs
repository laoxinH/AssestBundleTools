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

        text = ResolveText(preview);
        return !string.IsNullOrWhiteSpace(text);
    }

    public static string ResolveText(AssetPreviewDto preview) =>
        string.IsNullOrWhiteSpace(preview.FullTextContent)
            ? preview.TextContent ?? string.Empty
            : preview.FullTextContent;
}
