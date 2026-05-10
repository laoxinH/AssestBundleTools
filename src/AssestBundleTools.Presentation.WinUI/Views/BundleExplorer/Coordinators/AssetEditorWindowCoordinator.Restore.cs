using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private sealed partial class AssetEditorWindowCoordinator
    {
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
            var openSpineGroupKeys = CaptureOpenSpineGroupKeys();
            var restorePrompt = _owner.ViewModel.AssetEditorWindowRestorePrompt;
            restorePrompt.ReplacePotentialConflictDocumentKeys(openAssetKeys, openSpineGroupKeys);
            var restoreDraftMarkers = restorePrompt.RestoreDraftMarkers;
            var restorableDraftReferenceKeys = restorePrompt.CreateRestorableDraftReferenceKeys();
            var pendingWindows = restorePrompt.TakePendingSnapshots();
            if (pendingWindows.Count == 0)
            {
                return Task.CompletedTask;
            }

            try
            {
                var restorePlan = AssetEditorWindowRestorePlanner.CreatePlan(
                    pendingWindows,
                    CanRestoreAssetTab,
                    openAssetKeys,
                    CanRestoreSpineGroupTab,
                    openSpineGroupKeys);
                var remainingPendingWindows = restorePlan.PendingUnavailableWindows
                    .Concat(restorePlan.PendingConflictedWindows)
                    .ToList();
                var restoredWindowCount = 0;
                foreach (var pendingWindow in restorePlan.RestorableWindows)
                {
                    var restorableWindow = AssetEditorWindowRestoreDraftMarkerPolicy.Apply(
                        pendingWindow,
                        restoreDraftMarkers,
                        restorableDraftReferenceKeys);
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
                    CountDocumentTabs(restorePlan.PendingConflictedWindows));
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

        private void ClearPendingAssetEditorWindowRestoreState()
        {
            _owner.ViewModel.AssetEditorWindowRestorePrompt.Clear();
            _owner.ViewModel.AssetEditorWindowRestorePrompt.ReplacePotentialConflictDocumentKeys(null, null);
            RequestWindowStatePersistence();
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

        private IReadOnlySet<string> CaptureOpenSpineGroupKeys()
        {
            var openSpineGroupKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var bundleTab in _owner.ViewModel.DocumentWorkspace.BundleTabs)
            {
                foreach (var spineGroupTab in bundleTab.SpineGroupTabs)
                {
                    AddSpineGroupKey(openSpineGroupKeys, spineGroupTab.BundleId.Value, spineGroupTab.GroupId);
                }
            }

            foreach (var window in CaptureCurrentOpenWindowMetadata())
            {
                foreach (var documentTab in GetWindowDocumentTabs(window))
                {
                    if (documentTab.SpineGroupTab is { } spineGroupTab)
                    {
                        AddSpineGroupKey(openSpineGroupKeys, spineGroupTab.BundleId, spineGroupTab.GroupId);
                    }
                }
            }

            return openSpineGroupKeys;
        }

        private void RefreshPendingAssetEditorWindowConflictState()
        {
            _owner.ViewModel.AssetEditorWindowRestorePrompt.ReplacePotentialConflictDocumentKeys(
                CaptureOpenAssetKeys(),
                CaptureOpenSpineGroupKeys());
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
                        window.ActiveDocumentKind,
                        ":",
                        window.ActiveSpineGroupId,
                        ":",
                        CountDocumentTabs(window).ToString(System.Globalization.CultureInfo.InvariantCulture))));
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

            var restoredTabs = new List<AssetDocumentTabItemViewModel>(CountDocumentTabs(pendingWindow));
            foreach (var documentTabState in GetWindowDocumentTabs(pendingWindow))
            {
                if (documentTabState.AssetTab is { } assetTabState &&
                    CanRestoreAssetTab(assetTabState))
                {
                    restoredTabs.Add(AssetDocumentTabItemViewModel.FromAsset(CreateRestoredAssetTab(assetTabState)));
                }
                else if (documentTabState.SpineGroupTab is { } spineGroupTabState &&
                         CanRestoreSpineGroupTab(spineGroupTabState))
                {
                    restoredTabs.Add(AssetDocumentTabItemViewModel.FromSpineGroup(CreateRestoredSpineGroupTab(spineGroupTabState)));
                }
            }

            if (restoredTabs.Count == 0)
            {
                return null;
            }

            var activeDocumentTab = restoredTabs.FirstOrDefault(tab => IsActiveRestoredDocumentTab(pendingWindow, tab))
                               ?? restoredTabs[0];

            return new AssetDocumentGroupTransfer(restoredTabs, activeDocumentTab);
        }

        private AssetDocumentTabViewModel CreateRestoredAssetTab(
            WorkspaceAssetEditorWindowAssetTabDto assetTabState)
        {
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
            restoredAssetTab.RestoreInspectorScrollState(assetTabState.InspectorScrollState);
            return restoredAssetTab;
        }

        private SpineAssetGroupDocumentTabViewModel CreateRestoredSpineGroupTab(
            WorkspaceAssetEditorWindowSpineGroupTabDto spineGroupTabState)
        {
            var groupId = spineGroupTabState.GroupId.Trim();
            var restoredTab = _owner.ViewModel.AssetWorkflow.CreateSpineGroupDocumentTab(new SpineAssetGroupDto(
                GroupId: groupId,
                BundleId: new BundleId(spineGroupTabState.BundleId),
                DisplayName: string.IsNullOrWhiteSpace(spineGroupTabState.DisplayName)
                    ? groupId
                    : spineGroupTabState.DisplayName.Trim(),
                GroupKey: string.IsNullOrWhiteSpace(spineGroupTabState.GroupKey)
                    ? groupId
                    : spineGroupTabState.GroupKey.Trim(),
                SkeletonAssets: Array.Empty<SpineAssetGroupPartDto>(),
                AtlasAssets: Array.Empty<SpineAssetGroupPartDto>(),
                TexturePageAssets: Array.Empty<SpineAssetGroupPartDto>(),
                Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>()));
            restoredTab.RestorePreviewState(spineGroupTabState.PreviewState);
            restoredTab.RestoreInspectorScrollState(spineGroupTabState.InspectorScrollState);
            return restoredTab;
        }

        private bool CanRestoreAssetTab(WorkspaceAssetEditorWindowAssetTabDto assetTabState)
        {
            return !string.IsNullOrWhiteSpace(assetTabState.BundleId) &&
                   !string.IsNullOrWhiteSpace(assetTabState.AssetId) &&
                   _owner.ViewModel.BundlePane.FindBundleNode(new BundleId(assetTabState.BundleId)) is not null;
        }

        private bool CanRestoreSpineGroupTab(WorkspaceAssetEditorWindowSpineGroupTabDto spineGroupTabState)
        {
            return !string.IsNullOrWhiteSpace(spineGroupTabState.BundleId) &&
                   !string.IsNullOrWhiteSpace(spineGroupTabState.GroupId) &&
                   _owner.ViewModel.BundlePane.FindBundleNode(new BundleId(spineGroupTabState.BundleId)) is not null;
        }

        private static void AddAssetKey(HashSet<string> assetKeys, string? bundleId, string? assetId)
        {
            if (AssetDocumentIdentityKey.TryCreate(bundleId, assetId, out var assetKey))
            {
                assetKeys.Add(assetKey);
            }
        }

        private static void AddSpineGroupKey(HashSet<string> spineGroupKeys, string? bundleId, string? groupId)
        {
            if (SpineGroupDocumentIdentityKey.TryCreate(bundleId, groupId, out var spineGroupKey))
            {
                spineGroupKeys.Add(spineGroupKey);
            }
        }

        private static IReadOnlyList<WorkspaceAssetEditorWindowDocumentTabDto> GetWindowDocumentTabs(
            WorkspaceAssetEditorWindowStateDto window) =>
            window.OpenDocumentTabs is { Count: > 0 } documentTabs
                ? documentTabs
                : window.OpenAssetTabs
                    .Select(static assetTab => new WorkspaceAssetEditorWindowDocumentTabDto(
                        WorkspaceAssetEditorWindowDocumentTabDto.AssetKind,
                        AssetTab: assetTab))
                    .ToArray();

        private static bool IsActiveRestoredDocumentTab(
            WorkspaceAssetEditorWindowStateDto pendingWindow,
            AssetDocumentTabItemViewModel documentTab)
        {
            if (documentTab.AssetTab is { } assetTab)
            {
                return (string.IsNullOrWhiteSpace(pendingWindow.ActiveDocumentKind) ||
                        string.Equals(pendingWindow.ActiveDocumentKind, WorkspaceAssetEditorWindowDocumentTabDto.AssetKind, StringComparison.Ordinal)) &&
                       string.Equals(assetTab.BundleId.Value, pendingWindow.ActiveBundleId, StringComparison.Ordinal) &&
                       string.Equals(assetTab.AssetId.Value, pendingWindow.ActiveAssetId, StringComparison.Ordinal);
            }

            return documentTab.SpineGroupTab is { } spineGroupTab &&
                   string.Equals(pendingWindow.ActiveDocumentKind, WorkspaceAssetEditorWindowDocumentTabDto.SpineGroupKind, StringComparison.Ordinal) &&
                   string.Equals(spineGroupTab.BundleId.Value, pendingWindow.ActiveBundleId, StringComparison.Ordinal) &&
                   string.Equals(spineGroupTab.GroupId, pendingWindow.ActiveSpineGroupId, StringComparison.Ordinal);
        }

        private static int CountDocumentTabs(IReadOnlyList<WorkspaceAssetEditorWindowStateDto> windows) =>
            windows.Sum(CountDocumentTabs);

        private static int CountDocumentTabs(WorkspaceAssetEditorWindowStateDto window) =>
            GetWindowDocumentTabs(window).Count;
    }
}
