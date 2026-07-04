using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private sealed partial class AssetEditorWindowCoordinator
    {
        public async Task OpenTextAssetEditWindowAsync(AssetDocumentTabViewModel? assetTab)
        {
            if (assetTab?.Details is null || !CanUseTextEditor(assetTab.Details))
            {
                return;
            }

            if (await TryActivateOpenTextEditorWindowAsync(assetTab.BundleId, assetTab.AssetId))
            {
                return;
            }

            await OpenAssetDocumentInNewWindowAsync(assetTab);
            foreach (var window in _windows)
            {
                if (!window.ViewModel.DocumentGroup.TryGetAssetTab(
                        assetTab.BundleId,
                        assetTab.AssetId,
                        out _))
                {
                    continue;
                }

                await window.ViewModel.RequestTextEditModeForActiveAssetAsync();
                return;
            }
        }

        private async Task<bool> TryActivateOpenTextEditorWindowAsync(BundleId bundleId, AssetId assetId)
        {
            foreach (var window in _windows)
            {
                if (!window.ViewModel.DocumentGroup.TryGetAssetTab(bundleId, assetId, out var assetTab) ||
                    assetTab.Details is null ||
                    !CanUseTextEditor(assetTab.Details))
                {
                    continue;
                }

                await window.TryActivateAssetDocumentTabAsync(bundleId, assetId);
                await window.ViewModel.RequestTextEditModeForActiveAssetAsync();
                return true;
            }

            return false;
        }

        private static bool CanUseTextEditor(AssetDetailsDto details) =>
            details.AssetType == AssetType.TextAsset ||
            string.Equals(details.AssetTypeDisplayName, "Shader", StringComparison.OrdinalIgnoreCase);
    }
}
