using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

internal static class AssetDocumentOpenConflictResolver
{
    public static bool TryResolve(
        BundleId bundleId,
        AssetId assetId,
        IReadOnlyList<BundleDocumentTabViewModel> mainBundleTabs,
        IReadOnlyList<AssetDocumentGroupViewModel> editorWindowGroups,
        bool includeMainWindow,
        bool preferMainWindow,
        int? preferredEditorWindowIndex,
        int? excludedEditorWindowIndex,
        out AssetDocumentOpenConflictResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(mainBundleTabs);
        ArgumentNullException.ThrowIfNull(editorWindowGroups);

        if (includeMainWindow &&
            preferMainWindow &&
            TryResolveMainWindow(bundleId, assetId, mainBundleTabs, out resolution))
        {
            return true;
        }

        if (preferredEditorWindowIndex.HasValue &&
            TryResolveEditorWindow(
                bundleId,
                assetId,
                editorWindowGroups,
                preferredEditorWindowIndex.Value,
                excludedEditorWindowIndex,
                out resolution))
        {
            return true;
        }

        for (var index = 0; index < editorWindowGroups.Count; index++)
        {
            if (index == preferredEditorWindowIndex)
            {
                continue;
            }

            if (TryResolveEditorWindow(
                bundleId,
                assetId,
                editorWindowGroups,
                index,
                excludedEditorWindowIndex,
                out resolution))
            {
                return true;
            }
        }

        if (includeMainWindow &&
            !preferMainWindow &&
            TryResolveMainWindow(bundleId, assetId, mainBundleTabs, out resolution))
        {
            return true;
        }

        resolution = default;
        return false;
    }

    private static bool TryResolveMainWindow(
        BundleId bundleId,
        AssetId assetId,
        IReadOnlyList<BundleDocumentTabViewModel> mainBundleTabs,
        out AssetDocumentOpenConflictResolution resolution)
    {
        foreach (var bundleTab in mainBundleTabs)
        {
            if (!bundleTab.BundleId.Equals(bundleId) ||
                !bundleTab.TryGetAssetTab(assetId, out var assetTab))
            {
                continue;
            }

            resolution = AssetDocumentOpenConflictResolution.MainWindow(bundleTab, assetTab);
            return true;
        }

        resolution = default;
        return false;
    }

    private static bool TryResolveEditorWindow(
        BundleId bundleId,
        AssetId assetId,
        IReadOnlyList<AssetDocumentGroupViewModel> editorWindowGroups,
        int editorWindowIndex,
        int? excludedEditorWindowIndex,
        out AssetDocumentOpenConflictResolution resolution)
    {
        if (editorWindowIndex == excludedEditorWindowIndex ||
            editorWindowIndex < 0 ||
            editorWindowIndex >= editorWindowGroups.Count)
        {
            resolution = default;
            return false;
        }

        var documentGroup = editorWindowGroups[editorWindowIndex];
        if (!documentGroup.TryGetAssetTab(bundleId, assetId, out var assetTab))
        {
            resolution = default;
            return false;
        }

        resolution = AssetDocumentOpenConflictResolution.EditorWindow(editorWindowIndex, assetTab);
        return true;
    }
}

internal readonly record struct AssetDocumentOpenConflictResolution(
    AssetDocumentOpenConflictHostKind HostKind,
    int EditorWindowIndex,
    BundleDocumentTabViewModel? BundleTab,
    AssetDocumentTabViewModel AssetTab)
{
    public static AssetDocumentOpenConflictResolution MainWindow(
        BundleDocumentTabViewModel bundleTab,
        AssetDocumentTabViewModel assetTab) =>
        new(
            AssetDocumentOpenConflictHostKind.MainWindow,
            -1,
            bundleTab,
            assetTab);

    public static AssetDocumentOpenConflictResolution EditorWindow(
        int editorWindowIndex,
        AssetDocumentTabViewModel assetTab) =>
        new(
            AssetDocumentOpenConflictHostKind.EditorWindow,
            editorWindowIndex,
            null,
            assetTab);
}

internal enum AssetDocumentOpenConflictHostKind
{
    MainWindow,
    EditorWindow
}
