using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private sealed class AssetEditorWindowCoordinator
    {
        private readonly BundleExplorerPage _owner;
        private readonly List<AssetEditorWindow> _windows = [];

        public AssetEditorWindowCoordinator(BundleExplorerPage owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public async Task OpenAssetDocumentInNewWindowAsync(AssetDocumentTabViewModel? assetTab)
        {
            var detachedAssetTab = await _owner.ViewModel.DetachAssetDocumentTabForEditorWindowAsync(assetTab);
            if (detachedAssetTab is null)
            {
                return;
            }

            try
            {
                var window = CreateAssetEditorWindow(detachedAssetTab);
                window.Activate();
            }
            catch
            {
                await _owner.ViewModel.AttachAssetDocumentTabFromEditorWindowAsync(detachedAssetTab);
                throw;
            }
        }

        private async Task OpenAssetDocumentInNewWindowFromEditorAsync(
            AssetEditorWindow sourceWindow,
            AssetDocumentTabViewModel assetTab)
        {
            ArgumentNullException.ThrowIfNull(sourceWindow);
            ArgumentNullException.ThrowIfNull(assetTab);

            var sourceHost = (IAssetDocumentTabDragHost)sourceWindow;
            var detachedAssetTab = await sourceHost.DetachAssetDocumentTabForDragAsync(assetTab);
            if (detachedAssetTab is null)
            {
                return;
            }

            try
            {
                var window = CreateAssetEditorWindow(detachedAssetTab);
                window.Activate();
                await sourceHost.CompleteAssetDocumentTabDragMoveAsync();
            }
            catch
            {
                await sourceHost.AttachAssetDocumentTabFromDragAsync(detachedAssetTab);
                throw;
            }
        }

        public void CloseAll()
        {
            foreach (var window in _windows.ToArray())
            {
                window.Closed -= OnAssetEditorWindowClosed;
                window.Close();
            }

            _windows.Clear();
        }

        private void OnAssetEditorWindowClosed(object sender, WindowEventArgs args)
        {
            if (sender is not AssetEditorWindow window)
            {
                return;
            }

            window.Closed -= OnAssetEditorWindowClosed;
            _windows.Remove(window);
        }

        private AssetEditorWindow CreateAssetEditorWindow(AssetDocumentTabViewModel detachedAssetTab)
        {
            var viewModel = new AssetEditorWindowViewModel(
                detachedAssetTab,
                App.CurrentApp.WorkspaceAssetInspectionGateway,
                App.CurrentApp.WorkspaceAssetOperationGateway,
                App.CurrentApp.AppLogService,
                App.CurrentApp.AppNotificationService,
                App.CurrentApp.LocalizationService,
                App.CurrentApp.FilePickerService,
                App.CurrentApp.WorkspaceContextService,
                App.CurrentApp.BackgroundTaskRunner,
                App.CurrentApp.PathNavigationService,
                _owner.ViewModel.WorkspaceShell,
                _owner.ViewModel.BundlePane,
                _owner.ViewModel.AttachAssetDocumentTabFromEditorWindowAsync);
            var window = new AssetEditorWindow(
                viewModel,
                OpenAssetDocumentInNewWindowFromEditorAsync);
            window.Closed += OnAssetEditorWindowClosed;
            _windows.Add(window);
            return window;
        }
    }
}
