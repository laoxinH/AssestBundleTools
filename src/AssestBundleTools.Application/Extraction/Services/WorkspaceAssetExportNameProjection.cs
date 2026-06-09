using System.IO;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Extraction.Services;

/// <summary>
/// 统一投影普通资源的导出显示名和文件名，避免同名资产在列表和批量提取中分叉。
/// </summary>
public static class WorkspaceAssetExportNameProjection
{
    public static IReadOnlyList<AssetListItemDto> ApplyToAssetList(IReadOnlyList<AssetListItemDto> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            return items;
        }

        var projectedItems = items.ToArray();
        var projectionsByAssetId = projectedItems
            .GroupBy(CreateCollisionKey)
            .SelectMany(CreateGroupProjections)
            .ToDictionary(static projection => projection.AssetId.Value, StringComparer.Ordinal);

        for (var index = 0; index < projectedItems.Length; index++)
        {
            var item = projectedItems[index];
            if (!projectionsByAssetId.TryGetValue(item.AssetId.Value, out var projection))
            {
                continue;
            }

            projectedItems[index] = item with
            {
                DisplayName = projection.DisplayName,
                ExportFileName = projection.ExportFileName
            };
        }

        return projectedItems;
    }

    public static string ResolveAssetTypeFolderName(string? assetTypeDisplayName, AssetType assetType) =>
        string.IsNullOrWhiteSpace(assetTypeDisplayName)
            ? assetType.ToString()
            : assetTypeDisplayName.Trim();

    public static string GetAssetExtension(AssetType assetType) =>
        assetType switch
        {
            AssetType.Texture2D => ".png",
            AssetType.Sprite => ".png",
            AssetType.TextAsset => ".txt",
            AssetType.AudioClip => ".wav",
            AssetType.Mesh => ".obj",
            AssetType.Material => ".material.json",
            _ => ".bin"
        };

    public static string SanitizeFileName(string fileName)
    {
        var normalized = SanitizePathSegment(fileName);
        return string.IsNullOrWhiteSpace(normalized)
            ? "asset"
            : normalized;
    }

    public static string SanitizePathSegment(string segment)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string((segment ?? string.Empty)
            .Trim()
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray())
            .Trim()
            .TrimEnd('.');

        return string.IsNullOrWhiteSpace(sanitized)
            ? "bundle"
            : sanitized;
    }

    private static IEnumerable<AssetExportNameProjection> CreateGroupProjections(
        IGrouping<AssetExportNameCollisionKey, AssetListItemDto> group)
    {
        var orderedItems = group
            .OrderBy(static item => item.PathId)
            .ThenBy(static item => item.AssetId.Value, StringComparer.Ordinal)
            .ThenBy(static item => item.AssetPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (orderedItems.Length == 1)
        {
            var item = orderedItems[0];
            yield return new AssetExportNameProjection(
                item.AssetId,
                DisplayName: item.Name,
                ExportFileName: $"{SanitizeFileName(ResolveBaseName(item))}{GetAssetExtension(item.AssetType)}");
            yield break;
        }

        for (var index = 0; index < orderedItems.Length; index++)
        {
            var item = orderedItems[index];
            var suffix = $" [{index + 1}]";
            yield return new AssetExportNameProjection(
                item.AssetId,
                DisplayName: $"{ResolveBaseName(item)}{suffix}",
                ExportFileName: $"{SanitizeFileName(ResolveBaseName(item))}{suffix}{GetAssetExtension(item.AssetType)}");
        }
    }

    private static AssetExportNameCollisionKey CreateCollisionKey(AssetListItemDto item) =>
        new(
            item.BundleId.Value,
            ResolveAssetTypeFolderName(item.AssetTypeDisplayName, item.AssetType),
            SanitizeFileName(ResolveBaseName(item)),
            GetAssetExtension(item.AssetType));

    private static string ResolveBaseName(AssetListItemDto item) =>
        string.IsNullOrWhiteSpace(item.Name)
            ? item.AssetId.Value
            : item.Name.Trim();

    private sealed record AssetExportNameCollisionKey(
        string BundleId,
        string AssetTypeFolderName,
        string BaseFileName,
        string Extension);

    private sealed record AssetExportNameProjection(
        AssetId AssetId,
        string DisplayName,
        string ExportFileName);
}
