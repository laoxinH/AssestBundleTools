using System.Globalization;
using System.Text.Json;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Assets.Queries;

internal sealed partial class SpineAnimationPreviewBuilder
{
    private static bool TryGetSkinAttachmentsRoot(JsonElement root, out JsonElement attachmentsRoot)
    {
        attachmentsRoot = default;
        if (!root.TryGetProperty("skins", out var skinsElement))
        {
            return false;
        }

        if (skinsElement.ValueKind == JsonValueKind.Object)
        {
            if (skinsElement.TryGetProperty("default", out attachmentsRoot) &&
                attachmentsRoot.ValueKind == JsonValueKind.Object)
            {
                return true;
            }

            attachmentsRoot = skinsElement.EnumerateObject().FirstOrDefault().Value;
            return attachmentsRoot.ValueKind == JsonValueKind.Object;
        }

        if (skinsElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var selectedSkin = skinsElement.EnumerateArray().FirstOrDefault(IsDefaultSkin);
        if (selectedSkin.ValueKind == JsonValueKind.Undefined)
        {
            selectedSkin = skinsElement.EnumerateArray().FirstOrDefault();
        }

        return selectedSkin.ValueKind == JsonValueKind.Object &&
               selectedSkin.TryGetProperty("attachments", out attachmentsRoot) &&
               attachmentsRoot.ValueKind == JsonValueKind.Object;
    }

    private static bool IsDefaultSkin(JsonElement skin) =>
        skin.ValueKind == JsonValueKind.Object &&
        string.Equals(GetString(skin, "name"), "default", StringComparison.OrdinalIgnoreCase);

    private static bool IsRegionAttachment(JsonElement attachmentElement)
    {
        var type = GetString(attachmentElement, "type");
        return string.IsNullOrWhiteSpace(type) ||
               string.Equals(type, "region", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadSkeletonJson(
        Result<AssetPreviewDto> previewResult,
        out string skeletonJson)
    {
        skeletonJson = string.Empty;
        if (!previewResult.IsSuccess ||
            previewResult.Value is not { Kind: AssetPreviewKind.Text } preview ||
            string.IsNullOrWhiteSpace(preview.TextContent))
        {
            return false;
        }

        skeletonJson = preview.TextContent;
        return true;
    }

    private static bool TryGetArray(JsonElement element, string propertyName, out JsonElement property)
    {
        property = default;
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out property) &&
               property.ValueKind == JsonValueKind.Array;
    }

    private static bool TryGetObject(JsonElement element, string propertyName, out JsonElement property)
    {
        property = default;
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out property) &&
               property.ValueKind == JsonValueKind.Object;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static double GetDouble(JsonElement element, string propertyName, double fallback)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var property))
        {
            return fallback;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetDouble(out var value) => value,
            JsonValueKind.String when double.TryParse(
                property.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value) => value,
            _ => fallback
        };
    }

    private static string? ResolveSkeletonMetadataText(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty("skeleton", out var skeleton) ||
            skeleton.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetString(skeleton, propertyName);
    }

    private static string NormalizeRegionName(string? regionName) =>
        string.IsNullOrWhiteSpace(regionName)
            ? string.Empty
            : regionName.Trim().Replace('\\', '/');

    private static bool IsBinarySkeleton(SpineAssetGroupPartDto skeletonAsset)
    {
        var path = string.IsNullOrWhiteSpace(skeletonAsset.AssetPath)
            ? skeletonAsset.Name
            : skeletonAsset.AssetPath;
        return !string.IsNullOrWhiteSpace(path) &&
               path.Trim().EndsWith(".skel", StringComparison.OrdinalIgnoreCase);
    }
}
