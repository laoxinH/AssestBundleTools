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
    private sealed class AssetEditorWindowCoordinator
    {
        private readonly BundleExplorerPage _owner;
        private readonly List<AssetEditorWindow> _windows = [];
        private readonly Dictionary<AssetEditorWindow, int> _windowStateIds = [];
        private readonly AssetEditorWindowStateStore _windowStateStore = new();
        private string? _activeWorkspaceId;
        private string? _lastPromptedRestoreSnapshotKey;
        private bool _isRestorePromptDialogOpen;

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

        public async Task PromptPendingAssetEditorWindowsRestoreAsync()
        {
            var restorePrompt = _owner.ViewModel.AssetEditorWindowRestorePrompt;
            if (_isRestorePromptDialogOpen ||
                _owner.XamlRoot is null ||
                !_owner.ViewModel.WorkspaceShell.HasWorkspace ||
                !restorePrompt.HasPendingWindows)
            {
                return;
            }

            RefreshPendingAssetEditorWindowConflictState();
            await _owner.ViewModel.RefreshAssetEditorWindowDraftRestorePlanAsync();
            var promptKey = CreatePendingRestorePromptKey(
                restorePrompt.PendingWindows,
                restorePrompt.PendingConflictAssetTabCount,
                restorePrompt.DraftRestoreDiagnosticsKey);
            if (string.Equals(_lastPromptedRestoreSnapshotKey, promptKey, StringComparison.Ordinal))
            {
                return;
            }

            _lastPromptedRestoreSnapshotKey = promptKey;
            _isRestorePromptDialogOpen = true;
            try
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = _owner.XamlRoot,
                    Title = UiText.Get(ResourceKeys.AssetEditorWindowRestorePendingDialogTitle),
                    PrimaryButtonText = UiText.Get(ResourceKeys.AssetEditorWindowRestorePendingDialogPrimaryButton),
                    SecondaryButtonText = UiText.Get(ResourceKeys.AssetEditorWindowRestorePendingDialogSecondaryButton),
                    CloseButtonText = UiText.Get(ResourceKeys.AssetEditorWindowRestorePendingDialogCloseButton),
                    DefaultButton = ContentDialogButton.Close,
                    Content = AssetEditorWindowRestoreDialogContentBuilder.Create(restorePrompt)
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    await RestorePendingAssetEditorWindowsAsync();
                }
                else if (result == ContentDialogResult.Secondary)
                {
                    ClearPendingAssetEditorWindowRestoreState();
                }
            }
            finally
            {
                _isRestorePromptDialogOpen = false;
            }
        }

        public Task RestorePendingAssetEditorWindowsAsync()
        {
            if (!_owner.ViewModel.WorkspaceShell.HasWorkspace || _owner.ViewModel.IsBusy)
            {
                return Task.CompletedTask;
            }

            var openAssetKeys = CaptureOpenAssetKeys();
            _owner.ViewModel.AssetEditorWindowRestorePrompt.ReplacePotentialConflictAssetKeys(openAssetKeys);
            var restoreDraftMarkers = _owner.ViewModel.AssetEditorWindowRestorePrompt.RestoreDraftMarkers;
            var pendingWindows = _owner.ViewModel.AssetEditorWindowRestorePrompt.TakePendingSnapshots();
            if (pendingWindows.Count == 0)
            {
                return Task.CompletedTask;
            }

            try
            {
                var restorePlan = AssetEditorWindowRestorePlanner.CreatePlan(
                    pendingWindows,
                    CanRestoreAssetTab,
                    openAssetKeys);
                var remainingPendingWindows = restorePlan.PendingUnavailableWindows
                    .Concat(restorePlan.PendingConflictedWindows)
                    .ToList();
                var restoredWindowCount = 0;
                foreach (var pendingWindow in restorePlan.RestorableWindows)
                {
                    var restorableWindow = AssetEditorWindowRestoreDraftMarkerPolicy.Apply(
                        pendingWindow,
                        restoreDraftMarkers);
                    var restoredGroup = TryCreateRestoredDocumentGroup(restorableWindow);
                    if (restoredGroup is null)
                    {
                        remainingPendingWindows.Add(pendingWindow);
                        continue;
                    }

                    var restoredLayout = AssetEditorWindowRestorePlanner.CreateRestoredLayout(pendingWindow.Layout);
                    var window = CreateAssetEditorWindow(restoredGroup, restoredLayout);
                    window.Activate();
                    restoredWindowCount++;
                }

                _owner.ViewModel.AssetEditorWindowRestorePrompt.ReplacePendingSnapshots(remainingPendingWindows);
                RefreshPendingAssetEditorWindowConflictState();
                RequestWindowStatePersistence();
                AssetEditorWindowRestoreFeedbackPresenter.Publish(
                    restoredWindowCount,
                    restorePlan.PendingUnavailableWindows.Count,
                    restorePlan.PendingConflictedWindows.Count,
                    CountAssetTabs(restorePlan.PendingConflictedWindows));
            }
            catch
            {
                _owner.ViewModel.AssetEditorWindowRestorePrompt.ReplacePendingSnapshots(pendingWindows);
                RefreshPendingAssetEditorWindowConflictState();
                RequestWindowStatePersistence();
                throw;
            }

            return Task.CompletedTask;
        }

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

        private void ClearPendingAssetEditorWindowRestoreState()
        {
            _owner.ViewModel.AssetEditorWindowRestorePrompt.Clear();
            _owner.ViewModel.AssetEditorWindowRestorePrompt.ReplacePotentialConflictAssetKeys(null);
            RequestWindowStatePersistence();
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

        private AssetEditorWindow CreateAssetEditorWindow(
            AssetDocumentGroupTransfer detachedGroup,
            AssetEditorWindowLayoutSnapshot? preferredLayout = null)
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

        private IReadOnlySet<string> CaptureOpenAssetKeys()
        {
            var openAssetKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var bundleTab in _owner.ViewModel.DocumentWorkspace.BundleTabs)
            {
                foreach (var assetTab in bundleTab.AssetTabs)
                {
                    AddAssetKey(openAssetKeys, assetTab.BundleId.Value, assetTab.AssetId.Value);
                }
            }

            foreach (var window in CaptureCurrentOpenWindowMetadata())
            {
                foreach (var assetTab in window.OpenAssetTabs)
                {
                    AddAssetKey(openAssetKeys, assetTab.BundleId, assetTab.AssetId);
                }
            }

            return openAssetKeys;
        }

        private void RefreshPendingAssetEditorWindowConflictState()
        {
            _owner.ViewModel.AssetEditorWindowRestorePrompt.ReplacePotentialConflictAssetKeys(CaptureOpenAssetKeys());
        }

        private void RequestWindowStatePersistence()
        {
            _owner.ViewModel.RequestAssetEditorWindowStatePersistence();
        }

        private static string CreatePendingRestorePromptKey(
            IReadOnlyList<WorkspaceAssetEditorWindowStateDto> pendingWindows,
            int conflictedAssetTabCount,
            string draftRestoreDiagnosticsKey)
        {
            var snapshotKey = string.Join(
                "|",
                pendingWindows.Select(static window =>
                    string.Concat(
                        window.WindowStateId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ":",
                        window.ActiveBundleId,
                        ":",
                        window.ActiveAssetId,
                        ":",
                        window.OpenAssetTabs.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            return string.Concat(
                snapshotKey,
                ":conflicts:",
                conflictedAssetTabCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ":drafts:",
                draftRestoreDiagnosticsKey);
        }

        private AssetDocumentGroupTransfer? TryCreateRestoredDocumentGroup(
            WorkspaceAssetEditorWindowStateDto pendingWindow)
        {
            ArgumentNullException.ThrowIfNull(pendingWindow);

            var restoredTabs = new List<AssetDocumentTabViewModel>(pendingWindow.OpenAssetTabs.Count);
            foreach (var assetTabState in pendingWindow.OpenAssetTabs)
            {
                if (!CanRestoreAssetTab(assetTabState))
                {
                    continue;
                }

                var restoredAssetTab = _owner.ViewModel.AssetWorkflow.CreateAssetDocumentTab(
                    new BundleId(assetTabState.BundleId),
                    new AssetId(assetTabState.AssetId),
                    assetTabState.AssetName);
                restoredAssetTab.RestoreSelectedInspectorSection(assetTabState.SelectedInspectorSection);
                restoredAssetTab.RestorePreviewImageDisplayOptions(
                    assetTabState.PreviewImageBackgroundMode,
                    assetTabState.SpritePreviewDisplayMode,
                    assetTabState.PreviewImageZoom,
                    assetTabState.PreviewImagePanX,
                    assetTabState.PreviewImagePanY);
                restoredAssetTab.RestorePreviewMeshDisplayOptions(
                    assetTabState.MeshPreviewDisplayMode,
                    assetTabState.MeshPreviewTextureOverlaySelected,
                    assetTabState.MeshPreviewGridSelected,
                    assetTabState.MeshPreviewZoom,
                    assetTabState.MeshPreviewRotationX,
                    assetTabState.MeshPreviewRotationY);
                restoredAssetTab.RestoreEditDraftReference(
                    AssetDocumentEditDraftReferenceMapper.ToViewModel(assetTabState.EditDraftReference));
                restoredTabs.Add(restoredAssetTab);
            }

            if (restoredTabs.Count == 0)
            {
                return null;
            }

            var activeAssetTab = restoredTabs.FirstOrDefault(assetTab =>
                                   string.Equals(assetTab.BundleId.Value, pendingWindow.ActiveBundleId, StringComparison.Ordinal) &&
                                   string.Equals(assetTab.AssetId.Value, pendingWindow.ActiveAssetId, StringComparison.Ordinal))
                               ?? restoredTabs[0];

            return new AssetDocumentGroupTransfer(restoredTabs, activeAssetTab);
        }

        private bool CanRestoreAssetTab(WorkspaceAssetEditorWindowAssetTabDto assetTabState)
        {
            return !string.IsNullOrWhiteSpace(assetTabState.BundleId) &&
                   !string.IsNullOrWhiteSpace(assetTabState.AssetId) &&
                   _owner.ViewModel.BundlePane.FindBundleNode(new BundleId(assetTabState.BundleId)) is not null;
        }

        private static void AddAssetKey(HashSet<string> assetKeys, string? bundleId, string? assetId)
        {
            if (AssetDocumentIdentityKey.TryCreate(bundleId, assetId, out var assetKey))
            {
                assetKeys.Add(assetKey);
            }
        }

        private static int CountAssetTabs(IReadOnlyList<WorkspaceAssetEditorWindowStateDto> windows) =>
            windows.Sum(static window => window.OpenAssetTabs.Count);

    }
}
