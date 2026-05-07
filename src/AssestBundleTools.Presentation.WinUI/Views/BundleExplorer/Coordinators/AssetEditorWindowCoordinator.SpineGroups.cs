using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private sealed partial class AssetEditorWindowCoordinator
    {
        public async Task OpenSpineGroupDocumentInNewWindowAsync(
            SpineAssetGroupDocumentTabViewModel? spineGroupTab)
        {
            var detachedSpineGroupTab = await _owner.ViewModel.DetachSpineGroupDocumentTabForEditorWindowAsync(spineGroupTab);
            if (detachedSpineGroupTab is null)
            {
                return;
            }

            try
            {
                var window = CreateSpineGroupEditorWindow(detachedSpineGroupTab);
                window.Activate();
            }
            catch
            {
                await _owner.ViewModel.RestoreDetachedSpineGroupDocumentTabFromEditorWindowAsync(detachedSpineGroupTab);
                throw;
            }
        }

        private SpineAssetGroupEditorWindow CreateSpineGroupEditorWindow(
            SpineAssetGroupDocumentTabViewModel detachedSpineGroupTab)
        {
            var detailsLoader = new SpineAssetGroupDetailsLoader(
                App.CurrentApp.WorkspaceAssetInspectionGateway,
                App.CurrentApp.WorkspaceContextService,
                _owner.ViewModel.WorkspaceShell,
                App.CurrentApp.BackgroundTaskRunner);
            var viewModel = new SpineAssetGroupEditorWindowViewModel(
                detachedSpineGroupTab,
                _owner.ViewModel.BundlePane,
                App.CurrentApp.LocalizationService,
                detailsLoader);
            var window = new SpineAssetGroupEditorWindow(
                viewModel,
                target => _owner.ViewModel.OpenResolvedAssetAsync(target));
            window.Closed += OnSpineGroupEditorWindowClosed;
            _spineGroupWindows.Add(window);
            return window;
        }

        private void OnSpineGroupEditorWindowClosed(object sender, WindowEventArgs args)
        {
            if (sender is not SpineAssetGroupEditorWindow window)
            {
                return;
            }

            window.Closed -= OnSpineGroupEditorWindowClosed;
            _spineGroupWindows.Remove(window);
        }

        private void CloseAllSpineGroupWindows()
        {
            foreach (var window in _spineGroupWindows.ToArray())
            {
                window.Closed -= OnSpineGroupEditorWindowClosed;
                window.Close();
            }

            _spineGroupWindows.Clear();
        }
    }
}
