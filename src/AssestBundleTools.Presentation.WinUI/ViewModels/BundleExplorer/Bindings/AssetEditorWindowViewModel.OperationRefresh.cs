using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class AssetEditorWindowViewModel
{
    private async Task RefreshOperationAffectedStateAsync(WorkspaceOperationChangedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        var assetTabs = eventArgs.IsWorkspaceWide
            ? DocumentGroup.AssetTabs.ToArray()
            : DocumentGroup.AssetTabs
                .Where(assetTab => eventArgs.AffectedAssets.Any(affectedAsset =>
                    assetTab.BundleId.Equals(affectedAsset.BundleId) &&
                    assetTab.AssetId.Equals(affectedAsset.AssetId)))
                .ToArray();
        foreach (var assetTab in assetTabs)
        {
            await RefreshAssetDocumentDetailsAsync(assetTab);
        }

        var spineGroupTabs = eventArgs.IsWorkspaceWide
            ? DocumentGroup.SpineGroupTabs.ToArray()
            : DocumentGroup.SpineGroupTabs
                .Where(spineGroupTab => eventArgs.AffectedAssets.Any(affectedAsset =>
                    SpineGroupContainsAsset(spineGroupTab, affectedAsset)))
                .ToArray();
        foreach (var spineGroupTab in spineGroupTabs)
        {
            await RefreshSpineGroupDocumentDetailsAsync(spineGroupTab);
        }
    }

    private static bool SpineGroupContainsAsset(
        SpineAssetGroupDocumentTabViewModel spineGroupTab,
        WorkspaceOperationAffectedAsset affectedAsset) =>
        spineGroupTab.Item.SkeletonAssets
            .Concat(spineGroupTab.Item.AtlasAssets)
            .Concat(spineGroupTab.Item.TexturePageAssets)
            .Any(part =>
                part.BundleId.Equals(affectedAsset.BundleId) &&
                part.AssetId.Equals(affectedAsset.AssetId));
}
