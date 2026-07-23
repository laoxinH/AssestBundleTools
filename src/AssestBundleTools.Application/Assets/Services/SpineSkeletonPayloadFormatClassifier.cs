using System.Text;
using System.Text.Json;
using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Services;

/// <summary>
/// 按真实预览载荷识别 Spine skeleton 的 JSON 或二进制格式。
/// </summary>
public static class SpineSkeletonPayloadFormatClassifier
{
    public static SpineSkeletonAssetFormat Classify(AssetPreviewDto preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        return preview.Kind switch
        {
            AssetPreviewKind.Binary => SpineSkeletonAssetFormat.Binary,
            AssetPreviewKind.Text => ClassifyText(preview.FullTextContent ?? preview.TextContent),
            _ => SpineSkeletonAssetFormat.Unknown
        };
    }

    public static SpineSkeletonAssetFormat Classify(AssetPreviewContentDto preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (preview.Kind == AssetPreviewKind.Binary)
        {
            return SpineSkeletonAssetFormat.Binary;
        }

        if (preview.Kind != AssetPreviewKind.Text || preview.Content is not { Length: > 0 })
        {
            return SpineSkeletonAssetFormat.Unknown;
        }

        try
        {
            return ClassifyText(new UTF8Encoding(false, true).GetString(preview.Content));
        }
        catch (DecoderFallbackException)
        {
            return SpineSkeletonAssetFormat.Binary;
        }
    }

    private static SpineSkeletonAssetFormat ClassifyText(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return SpineSkeletonAssetFormat.Unknown;
        }

        try
        {
            using var document = JsonDocument.Parse(
                content.TrimStart('\uFEFF').TrimEnd('\0'),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? SpineSkeletonAssetFormat.Json
                : SpineSkeletonAssetFormat.Unknown;
        }
        catch (JsonException)
        {
            return SpineSkeletonAssetFormat.Unknown;
        }
    }
}
