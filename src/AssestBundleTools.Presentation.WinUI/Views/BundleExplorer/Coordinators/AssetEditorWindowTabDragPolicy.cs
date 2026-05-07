using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal static class AssetEditorWindowTabDragPolicy
{
    public static bool CanCreateNewWindowFromDroppedTab(
        AssetDocumentGroupViewModel sourceGroup,
        AssetDocumentTabViewModel assetTab)
    {
        ArgumentNullException.ThrowIfNull(sourceGroup);
        ArgumentNullException.ThrowIfNull(assetTab);

        return sourceGroup.AssetTabs.Contains(assetTab) &&
               sourceGroup.AssetTabs.Count > 1;
    }
}
