using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal static class AssetEditorWindowRestoreDraftMarkerPolicy
{
    public static WorkspaceAssetEditorWindowStateDto Apply(
        WorkspaceAssetEditorWindowStateDto windowState,
        bool restoreDraftMarkers,
        IReadOnlySet<AssetEditDraftReferenceKey>? restorableDraftReferenceKeys = null)
    {
        if (restoreDraftMarkers && restorableDraftReferenceKeys is null)
        {
            return windowState;
        }

        return windowState with
        {
            OpenAssetTabs = windowState.OpenAssetTabs
                .Select(assetTab => ShouldKeepDraftReference(assetTab, restoreDraftMarkers, restorableDraftReferenceKeys)
                    ? assetTab
                    : assetTab with { EditDraftReference = null })
                .ToArray(),
            OpenDocumentTabs = windowState.OpenDocumentTabs?
                .Select(documentTab => ApplyDocumentTab(documentTab, restoreDraftMarkers, restorableDraftReferenceKeys))
                .ToArray()
        };
    }

    private static WorkspaceAssetEditorWindowDocumentTabDto ApplyDocumentTab(
        WorkspaceAssetEditorWindowDocumentTabDto documentTab,
        bool restoreDraftMarkers,
        IReadOnlySet<AssetEditDraftReferenceKey>? restorableDraftReferenceKeys)
    {
        if (documentTab.AssetTab is not { } assetTab)
        {
            return documentTab;
        }

        return ShouldKeepDraftReference(assetTab, restoreDraftMarkers, restorableDraftReferenceKeys)
            ? documentTab
            : documentTab with { AssetTab = assetTab with { EditDraftReference = null } };
    }

    private static bool ShouldKeepDraftReference(
        WorkspaceAssetEditorWindowAssetTabDto assetTab,
        bool restoreDraftMarkers,
        IReadOnlySet<AssetEditDraftReferenceKey>? restorableDraftReferenceKeys)
    {
        if (assetTab.EditDraftReference is null)
        {
            return true;
        }

        if (!restoreDraftMarkers)
        {
            return false;
        }

        var draftReferenceKey = AssetEditDraftReferenceKey.Create(assetTab.EditDraftReference);
        return draftReferenceKey is not null &&
               restorableDraftReferenceKeys is not null &&
               restorableDraftReferenceKeys.Contains(draftReferenceKey);
    }
}
