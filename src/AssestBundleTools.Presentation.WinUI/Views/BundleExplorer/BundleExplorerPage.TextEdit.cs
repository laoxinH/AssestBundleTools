using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private async void OnInspectorEditRequested(object sender, EventArgs e)
    {
        await EditSelectedAssetAsync();
    }

    private async Task EditSelectedAssetAsync()
    {
        if (!TryGetActiveTextAssetDocumentTab(out var textAssetTab))
        {
            return;
        }

        await _assetEditorWindowCoordinator.OpenTextAssetEditWindowAsync(textAssetTab);
    }

    private bool TryGetActiveTextAssetDocumentTab(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AssetDocumentTabViewModel? assetTab)
    {
        assetTab = ViewModel.DocumentWorkspace.ActiveAssetDocumentTab;
        return assetTab?.Details is { } details &&
               (details.AssetType == AssetType.TextAsset ||
                string.Equals(details.AssetTypeDisplayName, "Shader", StringComparison.OrdinalIgnoreCase));
    }
}
