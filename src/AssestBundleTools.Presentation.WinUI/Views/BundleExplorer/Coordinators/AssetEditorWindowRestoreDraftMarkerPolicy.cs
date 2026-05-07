using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal static class AssetEditorWindowRestoreDraftMarkerPolicy
{
    public static WorkspaceAssetEditorWindowStateDto Apply(
        WorkspaceAssetEditorWindowStateDto windowState,
        bool restoreDraftMarkers)
    {
        if (restoreDraftMarkers)
        {
            return windowState;
        }

        return windowState with
        {
            OpenAssetTabs = windowState.OpenAssetTabs
                .Select(static assetTab => assetTab with { EditDraftReference = null })
                .ToArray()
        };
    }
}
