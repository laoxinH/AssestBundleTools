using System.IO;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;

/// <summary>
/// 解析资产窗格批量提取时的资源包级输出目录。
/// </summary>
internal static class BundleBatchExtractionOutputDirectoryResolver
{
    public static string Build(
        string selectedOutputDirectory,
        string? bundleDisplayName,
        BundleId bundleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedOutputDirectory);

        var bundleFolderName = !string.IsNullOrWhiteSpace(bundleDisplayName)
            ? bundleDisplayName.Trim()
            : ResolveBundleIdFallback(bundleId);
        return Path.Combine(selectedOutputDirectory.Trim(), SanitizePathSegment(bundleFolderName));
    }

    public static string BuildAssetTypeDirectory(
        string selectedOutputDirectory,
        string? bundleDisplayName,
        BundleId bundleId,
        string? assetTypeDisplayName,
        AssetType assetType)
    {
        return Path.Combine(
            Build(selectedOutputDirectory, bundleDisplayName, bundleId),
            SanitizePathSegment(ResolveAssetTypeFolderName(assetTypeDisplayName, assetType)));
    }

    private static string ResolveBundleIdFallback(BundleId bundleId) =>
        string.IsNullOrWhiteSpace(bundleId.Value)
            ? "bundle"
            : bundleId.Value.Trim();

    private static string ResolveAssetTypeFolderName(string? assetTypeDisplayName, AssetType assetType) =>
        string.IsNullOrWhiteSpace(assetTypeDisplayName)
            ? assetType.ToString()
            : assetTypeDisplayName.Trim();

    private static string SanitizePathSegment(string segment)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(segment
            .Trim()
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray())
            .Trim()
            .TrimEnd('.');

        return string.IsNullOrWhiteSpace(sanitized)
            ? "bundle"
            : sanitized;
    }
}
