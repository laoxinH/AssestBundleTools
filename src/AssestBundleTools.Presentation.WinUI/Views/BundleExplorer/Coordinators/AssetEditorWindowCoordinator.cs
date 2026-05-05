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
                var window = new AssetEditorWindow(viewModel);
                window.Closed += OnAssetEditorWindowClosed;
                _windows.Add(window);
                window.Activate();
            }
            catch
            {
                await _owner.ViewModel.AttachAssetDocumentTabFromEditorWindowAsync(detachedAssetTab);
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
    }
}
