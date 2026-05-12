using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;

/// <summary>
/// 解析 Spine 资源组批量提取时的包级输出目录。
/// </summary>
internal static class SpineBatchExtractionOutputDirectoryResolver
{
    public static string Build(
        string selectedOutputDirectory,
        SpineAssetGroupEntryViewModel entry,
        string defaultBundleDisplayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedOutputDirectory);
        ArgumentNullException.ThrowIfNull(entry);

        return BundleBatchExtractionOutputDirectoryResolver.Build(
            selectedOutputDirectory,
            ResolveBundleDisplayName(entry, defaultBundleDisplayName),
            entry.Item.BundleId);
    }

    private static string ResolveBundleDisplayName(
        SpineAssetGroupEntryViewModel entry,
        string defaultBundleDisplayName)
    {
        var partBundleDisplayName = entry.Item.SkeletonAssets
            .Concat(entry.Item.AtlasAssets)
            .Concat(entry.Item.TexturePageAssets)
            .Where(part => part.BundleId == entry.Item.BundleId)
            .Select(static part => part.BundleDisplayName)
            .FirstOrDefault(static displayName => !string.IsNullOrWhiteSpace(displayName));
        if (!string.IsNullOrWhiteSpace(partBundleDisplayName))
        {
            return partBundleDisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(defaultBundleDisplayName))
        {
            return defaultBundleDisplayName.Trim();
        }

        return string.IsNullOrWhiteSpace(entry.Item.BundleId.Value)
            ? "bundle"
            : entry.Item.BundleId.Value.Trim();
    }

}
