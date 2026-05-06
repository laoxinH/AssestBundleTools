using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private sealed class AssetEditorWindowCoordinator
    {
        private readonly BundleExplorerPage _owner;
        private readonly List<AssetEditorWindow> _windows = [];
        private readonly Dictionary<AssetEditorWindow, int> _windowStateIds = [];
        private readonly AssetEditorWindowStateStore _windowStateStore = new();

        public AssetEditorWindowCoordinator(BundleExplorerPage owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public IReadOnlyList<WorkspaceAssetEditorWindowStateDto> CaptureOpenWindowMetadata() =>
            CaptureCurrentOpenWindowMetadata();

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

        public async Task OpenActiveAssetDocumentGroupInNewWindowAsync()
        {
            var detachedGroup = await _owner.ViewModel.DetachActiveAssetDocumentGroupForEditorWindowAsync();
            if (detachedGroup is null)
            {
                return;
            }

            try
            {
                var window = CreateAssetEditorWindow(detachedGroup);
                window.Activate();
            }
            catch
            {
                await _owner.ViewModel.AttachAssetDocumentGroupFromEditorWindowAsync(detachedGroup);
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

        private async Task OpenAssetDocumentGroupInNewWindowFromEditorAsync(AssetEditorWindow sourceWindow)
        {
            ArgumentNullException.ThrowIfNull(sourceWindow);

            var detachedGroup = await sourceWindow.ViewModel.DetachAssetDocumentGroupForExternalHostAsync();
            if (detachedGroup is null)
            {
                return;
            }

            try
            {
                var window = CreateAssetEditorWindow(detachedGroup);
                window.Activate();
                await sourceWindow.ViewModel.CompleteExternalAssetDocumentMoveAsync();
            }
            catch
            {
                await sourceWindow.ViewModel.RestoreDetachedAssetDocumentGroupAsync(detachedGroup);
                throw;
            }
        }

        public void CloseAll()
        {
            foreach (var window in _windows.ToArray())
            {
                window.Closed -= OnAssetEditorWindowClosed;
                window.LayoutSnapshotChanged -= OnAssetEditorWindowLayoutSnapshotChanged;
                window.GroupSnapshotChanged -= OnAssetEditorWindowGroupSnapshotChanged;
                window.Close();
            }

            _windows.Clear();
            _windowStateIds.Clear();
            _windowStateStore.Clear();
        }

        private void OnAssetEditorWindowClosed(object sender, WindowEventArgs args)
        {
            if (sender is not AssetEditorWindow window)
            {
                return;
            }

            window.Closed -= OnAssetEditorWindowClosed;
            window.LayoutSnapshotChanged -= OnAssetEditorWindowLayoutSnapshotChanged;
            window.GroupSnapshotChanged -= OnAssetEditorWindowGroupSnapshotChanged;
            if (_windowStateIds.Remove(window, out var windowStateId))
            {
                _windowStateStore.RemoveWindow(windowStateId);
            }

            _windows.Remove(window);
            RequestWindowStatePersistence();
        }

        private void OnAssetEditorWindowLayoutSnapshotChanged(
            AssetEditorWindow window,
            AssetEditorWindowLayoutSnapshot snapshot)
        {
            if (_windowStateIds.TryGetValue(window, out var windowStateId))
            {
                _windowStateStore.UpdateWindowLayout(windowStateId, snapshot);
                RequestWindowStatePersistence();
            }
        }

        private void OnAssetEditorWindowGroupSnapshotChanged(
            AssetEditorWindow window,
            AssetEditorWindowGroupSnapshot snapshot)
        {
            if (_windowStateIds.TryGetValue(window, out var windowStateId))
            {
                _windowStateStore.UpdateWindowGroup(windowStateId, snapshot);
                RequestWindowStatePersistence();
            }
        }

        private AssetEditorWindow CreateAssetEditorWindow(AssetDocumentTabViewModel detachedAssetTab)
        {
            return CreateAssetEditorWindow(new AssetDocumentGroupTransfer([detachedAssetTab], detachedAssetTab));
        }

        private AssetEditorWindow CreateAssetEditorWindow(AssetDocumentGroupTransfer detachedGroup)
        {
            var viewModel = new AssetEditorWindowViewModel(
                detachedGroup,
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
                _owner.ViewModel.AttachAssetDocumentTabFromEditorWindowAsync,
                _owner.ViewModel.AttachAssetDocumentGroupFromEditorWindowAsync);
            var window = new AssetEditorWindow(
                viewModel,
                OpenAssetDocumentInNewWindowFromEditorAsync,
                OpenAssetDocumentGroupInNewWindowFromEditorAsync);
            var initialLayout = _windowStateStore.CreateNextWindowLayout();
            var windowStateId = _windowStateStore.RegisterWindow(
                initialLayout,
                window.CaptureGroupSnapshot());
            _windowStateIds[window] = windowStateId;
            window.Closed += OnAssetEditorWindowClosed;
            window.LayoutSnapshotChanged += OnAssetEditorWindowLayoutSnapshotChanged;
            window.GroupSnapshotChanged += OnAssetEditorWindowGroupSnapshotChanged;
            window.ApplyInitialLayout(initialLayout);
            _windows.Add(window);
            RequestWindowStatePersistence();
            return window;
        }

        private IReadOnlyList<WorkspaceAssetEditorWindowStateDto> CaptureCurrentOpenWindowMetadata()
        {
            foreach (var window in _windows)
            {
                if (!_windowStateIds.TryGetValue(window, out var windowStateId))
                {
                    continue;
                }

                if (window.TryCaptureLayoutSnapshot(out var layout))
                {
                    _windowStateStore.UpdateWindowLayout(windowStateId, layout);
                }

                _windowStateStore.UpdateWindowGroup(windowStateId, window.CaptureGroupSnapshot());
            }

            return _windowStateStore.CaptureOpenWindowMetadata();
        }

        private void RequestWindowStatePersistence()
        {
            _owner.ViewModel.RequestAssetEditorWindowStatePersistence();
        }
    }
}
