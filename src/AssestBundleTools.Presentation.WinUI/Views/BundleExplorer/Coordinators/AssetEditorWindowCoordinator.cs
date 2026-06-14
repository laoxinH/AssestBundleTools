using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private sealed partial class AssetEditorWindowCoordinator
    {
        private readonly BundleExplorerPage _owner;
        private readonly List<AssetEditorWindow> _windows = [];
        private readonly Dictionary<AssetEditorWindow, int> _windowStateIds = [];
        private readonly AssetDocumentOpenConflictActivator _openConflictActivator;
        private readonly AssetEditorWindowStateStore _windowStateStore = new();
        private string? _activeWorkspaceId;
        private string? _lastPromptedRestoreSnapshotKey;
        private bool _isRestorePromptDialogOpen;

        public AssetEditorWindowCoordinator(BundleExplorerPage owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _openConflictActivator = new AssetDocumentOpenConflictActivator(_owner, () => _windows);
        }

        public IReadOnlyList<WorkspaceAssetEditorWindowStateDto> CaptureOpenWindowMetadata() =>
            CaptureCurrentOpenWindowMetadata();

        public async Task OpenAssetDocumentInNewWindowAsync(AssetDocumentTabViewModel? assetTab)
        {
            if (await _openConflictActivator.TryActivateForNewWindowFromMainAsync(assetTab))
            {
                return;
            }

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

        public async Task OpenSpriteAssetEditWindowAsync(AssetDocumentTabViewModel? assetTab)
        {
            if (assetTab?.Details?.AssetType != AssetType.Sprite)
            {
                return;
            }

            if (await TryActivateOpenSpriteEditorWindowAsync(assetTab.BundleId, assetTab.AssetId))
            {
                return;
            }

            assetTab.RequestSpriteEditMode();
            await OpenAssetDocumentInNewWindowAsync(assetTab);
        }

        public async Task OpenSpriteAssetEditWindowAsync(BundleId bundleId, AssetId assetId)
        {
            if (await TryActivateOpenSpriteEditorWindowAsync(bundleId, assetId))
            {
                return;
            }

            var assetTab = _owner.ViewModel.DocumentWorkspace.ActiveAssetDocumentTab;
            if (assetTab is not null &&
                assetTab.BundleId.Equals(bundleId) &&
                assetTab.AssetId.Equals(assetId))
            {
                await OpenSpriteAssetEditWindowAsync(assetTab);
            }
        }

        public async Task OpenSpineGroupEditWindowAsync(SpineAssetGroupDocumentTabViewModel? spineGroupTab)
        {
            if (spineGroupTab is null)
            {
                return;
            }

            if (await TryActivateOpenSpineEditorWindowAsync(spineGroupTab.BundleId, spineGroupTab.GroupId))
            {
                return;
            }

            await OpenDocumentInNewWindowAsync(AssetDocumentTabItemViewModel.FromSpineGroup(spineGroupTab));
            foreach (var window in _windows)
            {
                if (!window.ViewModel.DocumentGroup.TryGetSpineGroupTab(
                        spineGroupTab.BundleId,
                        spineGroupTab.GroupId,
                        out _))
                {
                    continue;
                }

                window.ViewModel.RequestSpineEditModeForActiveGroup();
                return;
            }
        }

        public async Task OpenDocumentInNewWindowAsync(AssetDocumentTabItemViewModel? documentTab)
        {
            if (documentTab?.AssetTab is { } assetTab)
            {
                await OpenAssetDocumentInNewWindowAsync(assetTab);
                return;
            }

            if (documentTab?.SpineGroupTab is not { } spineGroupTab)
            {
                return;
            }

            if (await _openConflictActivator.TryActivateSpineGroupForNewWindowFromMainAsync(spineGroupTab))
            {
                return;
            }

            var detachedSpineGroupTab = await _owner.ViewModel.DetachSpineGroupDocumentTabForEditorWindowAsync(spineGroupTab);
            if (detachedSpineGroupTab is null)
            {
                return;
            }

            var detachedDocumentTab = AssetDocumentTabItemViewModel.FromSpineGroup(detachedSpineGroupTab);
            var detachedGroup = new AssetDocumentGroupTransfer([detachedDocumentTab], detachedDocumentTab);
            try
            {
                var window = CreateAssetEditorWindow(detachedGroup);
                window.Activate();
            }
            catch
            {
                await _owner.ViewModel.RestoreDetachedSpineGroupDocumentTabFromEditorWindowAsync(detachedSpineGroupTab);
                throw;
            }
        }

        public Task HandleMainAssetSelectionChangedAsync() =>
            _openConflictActivator.HandleMainAssetSelectionChangedAsync();

        public Task HandleMainResolvedAssetOpenRequestedAsync(ResolvedAssetNavigationTarget? target) =>
            _openConflictActivator.HandleMainResolvedAssetOpenRequestedAsync(target);

        public void HandleWorkspaceShellPageStateChanged(
            BundleExplorerPageStateChangedEventArgs<BundleExplorerWorkspaceShellPageStateChange> eventArgs)
        {
            ArgumentNullException.ThrowIfNull(eventArgs);

            if (!eventArgs.ChangeFlags.HasFlag(BundleExplorerWorkspaceShellPageStateChange.WorkspaceAvailability))
            {
                return;
            }

            var nextWorkspaceId = _owner.ViewModel.WorkspaceShell.WorkspaceSummary?.WorkspaceId.Value;
            if (string.Equals(_activeWorkspaceId, nextWorkspaceId, StringComparison.Ordinal))
            {
                return;
            }

            CloseAll();
            _lastPromptedRestoreSnapshotKey = null;
            _activeWorkspaceId = nextWorkspaceId;
        }

        public void HandleDocumentWorkspacePageStateChanged(
            BundleExplorerPageStateChangedEventArgs<BundleExplorerDocumentWorkspacePageStateChange> eventArgs)
        {
            ArgumentNullException.ThrowIfNull(eventArgs);

            if (!eventArgs.ChangeFlags.HasFlag(BundleExplorerDocumentWorkspacePageStateChange.AssetDocumentHost))
            {
                return;
            }

            RefreshPendingAssetEditorWindowConflictState();
        }

        private async Task OpenDocumentInNewWindowFromEditorAsync(
            AssetEditorWindow sourceWindow,
            AssetDocumentTabItemViewModel documentTab)
        {
            ArgumentNullException.ThrowIfNull(sourceWindow);
            ArgumentNullException.ThrowIfNull(documentTab);

            if (sourceWindow.ViewModel.DocumentGroup.DocumentTabs.Count <= 1 &&
                sourceWindow.ViewModel.DocumentGroup.DocumentTabs.Contains(documentTab))
            {
                await sourceWindow.ViewModel.ActivateDocumentTabAsync(documentTab);
                sourceWindow.BringToFront();
                return;
            }

            if (documentTab.AssetTab is { } assetTab &&
                await _openConflictActivator.TryActivateForNewWindowFromEditorAsync(sourceWindow, assetTab))
            {
                return;
            }

            if (documentTab.SpineGroupTab is { } spineGroupTab &&
                await _openConflictActivator.TryActivateSpineGroupForNewWindowFromEditorAsync(sourceWindow, spineGroupTab))
            {
                return;
            }

            var detachedDocumentTab = await sourceWindow.ViewModel.DetachDocumentTabForExternalHostAsync(documentTab);
            if (detachedDocumentTab is null)
            {
                return;
            }

            try
            {
                var window = CreateAssetEditorWindow(new AssetDocumentGroupTransfer([detachedDocumentTab], detachedDocumentTab));
                window.Activate();
                await sourceWindow.ViewModel.CompleteExternalAssetDocumentMoveAsync();
            }
            catch
            {
                await sourceWindow.ViewModel.RestoreDetachedDocumentTabAsync(detachedDocumentTab);
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
                window.CloseWithoutReturningToMainWindow();
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
            RefreshPendingAssetEditorWindowConflictState();
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
                RefreshPendingAssetEditorWindowConflictState();
                RequestWindowStatePersistence();
            }
        }

        private AssetEditorWindow CreateAssetEditorWindow(AssetDocumentTabViewModel detachedAssetTab)
        {
            return CreateAssetEditorWindow(new AssetDocumentGroupTransfer([detachedAssetTab], detachedAssetTab));
        }

        private async Task<bool> TryActivateOpenSpriteEditorWindowAsync(BundleId bundleId, AssetId assetId)
        {
            foreach (var window in _windows)
            {
                if (!window.ViewModel.DocumentGroup.TryGetAssetTab(bundleId, assetId, out var assetTab) ||
                    assetTab.Details?.AssetType != AssetType.Sprite)
                {
                    continue;
                }

                await window.TryActivateAssetDocumentTabAsync(bundleId, assetId);
                window.ViewModel.RequestSpriteEditModeForActiveAsset();
                return true;
            }

            return false;
        }

        private async Task<bool> TryActivateOpenSpineEditorWindowAsync(BundleId bundleId, string groupId)
        {
            foreach (var window in _windows)
            {
                if (!window.ViewModel.DocumentGroup.TryGetSpineGroupTab(bundleId, groupId, out var spineGroupTab))
                {
                    continue;
                }

                await window.TryActivateSpineGroupDocumentTabAsync(bundleId, groupId);
                window.ViewModel.RequestSpineEditModeForActiveGroup();
                return true;
            }

            return false;
        }

        private AssetEditorWindow CreateAssetEditorWindow(
            AssetDocumentGroupTransfer detachedGroup,
            AssetEditorWindowLayoutSnapshot? preferredLayout = null)
        {
            var editorNotificationService = new AppNotificationService();
            var viewModel = new AssetEditorWindowViewModel(
                detachedGroup,
                App.CurrentApp.WorkspaceAssetInspectionGateway,
                App.CurrentApp.WorkspaceAssetOperationGateway,
                App.CurrentApp.WorkspaceOperationChangeNotifier,
                App.CurrentApp.AppLogService,
                editorNotificationService,
                App.CurrentApp.LocalizationService,
                App.CurrentApp.FilePickerService,
                App.CurrentApp.WorkspaceContextService,
                App.CurrentApp.BackgroundTaskRunner,
                App.CurrentApp.PathNavigationService,
                _owner.ViewModel.WorkspaceShell,
                _owner.ViewModel.BundlePane,
                _owner.ViewModel.AttachAssetDocumentTabFromEditorWindowAsync,
                _owner.ViewModel.AttachAssetDocumentGroupFromEditorWindowAsync,
                App.CurrentApp.AppBackgroundTaskStatusService,
                App.CurrentApp.BackgroundWorkScheduler,
                () => App.CurrentApp.AppPreferencesService.PreviewWorkerCount,
                _owner.ViewModel.AssetWorkflow.OperationPendingTracker);
            var window = new AssetEditorWindow(
                viewModel,
                OpenDocumentInNewWindowFromEditorAsync,
                OpenAssetDocumentGroupInNewWindowFromEditorAsync,
                _openConflictActivator.TryActivateFromEditorAsync);
            App.CurrentApp.RegisterAuxiliaryWindow(window);
            var initialLayout = preferredLayout is { } layout && AssetEditorWindowLayoutSafety.CanRestoreSize(layout)
                ? layout
                : _windowStateStore.CreateNextWindowLayout();
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
