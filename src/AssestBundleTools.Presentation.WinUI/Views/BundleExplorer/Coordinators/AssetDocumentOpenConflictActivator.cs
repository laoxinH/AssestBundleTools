using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.Views;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal sealed class AssetDocumentOpenConflictActivator
{
    private readonly BundleExplorerPage _owner;
    private readonly Func<IReadOnlyList<AssetEditorWindow>> _getEditorWindows;

    public AssetDocumentOpenConflictActivator(
        BundleExplorerPage owner,
        Func<IReadOnlyList<AssetEditorWindow>> getEditorWindows)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _getEditorWindows = getEditorWindows ?? throw new ArgumentNullException(nameof(getEditorWindows));
    }

    public Task<bool> TryActivateFromMainAsync(ResolvedAssetNavigationTarget? target) =>
        target is null
            ? Task.FromResult(false)
            : TryActivateAsync(
                target.BundleId,
                target.AssetId,
                includeMainWindow: true,
                preferMainWindow: true,
                preferredEditorWindow: null,
                excludedEditorWindow: null);

    public Task<bool> TryActivateFromMainAsync(BundleId bundleId, AssetId assetId) =>
        TryActivateAsync(
            bundleId,
            assetId,
            includeMainWindow: true,
            preferMainWindow: true,
            preferredEditorWindow: null,
            excludedEditorWindow: null);

    public async Task HandleMainAssetSelectionChangedAsync()
    {
        if (_owner.ViewModel.AssetWorkflow.PrimaryPane.ViewMode == AssetPaneViewMode.SpineGroups)
        {
            if (_owner.ViewModel.AssetWorkflow.PrimaryPane.SelectedSpineGroup?.Item is { } selectedGroup &&
                await TryActivateSpineGroupAsync(
                    selectedGroup.BundleId,
                    selectedGroup.GroupId,
                    includeMainWindow: false,
                    excludedEditorWindow: null))
            {
                _owner.ViewModel.AssetWorkflow.CaptureActiveTabState();
                return;
            }

            await _owner.ViewModel.HandlePrimaryPaneSelectionChangedAsync();
            return;
        }

        if (_owner.ViewModel.AssetWorkflow.PrimaryPane.ViewMode != AssetPaneViewMode.Assets)
        {
            await _owner.ViewModel.HandlePrimaryPaneSelectionChangedAsync();
            return;
        }

        if (_owner.ViewModel.AssetWorkflow.PrimaryPane.SelectedAsset?.Item is { } selectedAsset &&
            await TryActivateAsync(
                selectedAsset.BundleId,
                selectedAsset.AssetId,
                includeMainWindow: false,
                preferMainWindow: false,
                preferredEditorWindow: null,
                excludedEditorWindow: null))
        {
            _owner.ViewModel.AssetWorkflow.CaptureActiveTabState();
            return;
        }

        await _owner.ViewModel.HandlePrimaryPaneSelectionChangedAsync();
    }

    public async Task HandleMainResolvedAssetOpenRequestedAsync(ResolvedAssetNavigationTarget? target)
    {
        if (await TryActivateFromMainAsync(target))
        {
            return;
        }

        await _owner.ViewModel.OpenResolvedAssetAsync(target);
    }

    public Task<bool> TryActivateForNewWindowFromMainAsync(AssetDocumentTabViewModel? assetTab) =>
        assetTab is null
            ? Task.FromResult(false)
            : TryActivateAsync(
                assetTab.BundleId,
                assetTab.AssetId,
                includeMainWindow: false,
                preferMainWindow: false,
                preferredEditorWindow: null,
                excludedEditorWindow: null);

    public Task<bool> TryActivateForNewWindowFromEditorAsync(
        AssetEditorWindow sourceWindow,
        AssetDocumentTabViewModel? assetTab)
    {
        ArgumentNullException.ThrowIfNull(sourceWindow);

        return assetTab is null
            ? Task.FromResult(false)
            : TryActivateAsync(
                assetTab.BundleId,
                assetTab.AssetId,
                includeMainWindow: true,
                preferMainWindow: false,
                preferredEditorWindow: null,
                excludedEditorWindow: sourceWindow);
    }

    public Task<bool> TryActivateSpineGroupForNewWindowFromMainAsync(
        SpineAssetGroupDocumentTabViewModel? spineGroupTab)
    {
        return spineGroupTab is null
            ? Task.FromResult(false)
            : TryActivateSpineGroupAsync(
                spineGroupTab.BundleId,
                spineGroupTab.GroupId,
                includeMainWindow: false,
                excludedEditorWindow: null);
    }

    public Task<bool> TryActivateSpineGroupForNewWindowFromEditorAsync(
        AssetEditorWindow sourceWindow,
        SpineAssetGroupDocumentTabViewModel? spineGroupTab)
    {
        ArgumentNullException.ThrowIfNull(sourceWindow);

        return spineGroupTab is null
            ? Task.FromResult(false)
            : TryActivateSpineGroupAsync(
                spineGroupTab.BundleId,
                spineGroupTab.GroupId,
                includeMainWindow: true,
                excludedEditorWindow: sourceWindow);
    }

    public Task<bool> TryActivateFromEditorAsync(
        AssetEditorWindow sourceWindow,
        ResolvedAssetNavigationTarget? target)
    {
        ArgumentNullException.ThrowIfNull(sourceWindow);

        return target is null
            ? Task.FromResult(false)
            : TryActivateAsync(
                target.BundleId,
                target.AssetId,
                includeMainWindow: true,
                preferMainWindow: false,
                preferredEditorWindow: sourceWindow,
                excludedEditorWindow: null);
    }

    private async Task<bool> TryActivateAsync(
        BundleId bundleId,
        AssetId assetId,
        bool includeMainWindow,
        bool preferMainWindow,
        AssetEditorWindow? preferredEditorWindow,
        AssetEditorWindow? excludedEditorWindow)
    {
        var editorWindows = _getEditorWindows();
        var editorWindowGroups = editorWindows
            .Select(static window => window.ViewModel.DocumentGroup)
            .ToArray();
        if (!AssetDocumentOpenConflictResolver.TryResolve(
                bundleId,
                assetId,
                _owner.ViewModel.DocumentWorkspace.BundleTabs,
                editorWindowGroups,
                includeMainWindow,
                preferMainWindow,
                IndexOf(editorWindows, preferredEditorWindow),
                IndexOf(editorWindows, excludedEditorWindow),
                out var resolution))
        {
            return false;
        }

        if (resolution.HostKind == AssetDocumentOpenConflictHostKind.MainWindow)
        {
            if (resolution.BundleTab is null)
            {
                return false;
            }

            await _owner.ViewModel.ActivateBundleTabAsync(resolution.BundleTab);
            await _owner.ViewModel.ActivateAssetDocumentTabAsync(resolution.AssetTab);
            SynchronizeMainWindowSelection(resolution.BundleTab, resolution.AssetTab);
            TryBringMainWindowToFront();
            return true;
        }

        if (resolution.EditorWindowIndex < 0 ||
            resolution.EditorWindowIndex >= editorWindows.Count)
        {
            return false;
        }

        return await editorWindows[resolution.EditorWindowIndex].TryActivateAssetDocumentTabAsync(
            bundleId,
            assetId);
    }

    private async Task<bool> TryActivateSpineGroupAsync(
        BundleId bundleId,
        string groupId,
        bool includeMainWindow,
        AssetEditorWindow? excludedEditorWindow)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return false;
        }

        if (includeMainWindow &&
            TryFindMainSpineGroupDocumentTab(bundleId, groupId, out var bundleTab, out var spineGroupTab))
        {
            await _owner.ViewModel.ActivateBundleTabAsync(bundleTab);
            await _owner.ViewModel.ActivateSpineGroupDocumentTabAsync(spineGroupTab);
            SynchronizeMainWindowSelection(bundleTab, spineGroupTab);
            TryBringMainWindowToFront();
            return true;
        }

        foreach (var editorWindow in _getEditorWindows())
        {
            if (ReferenceEquals(editorWindow, excludedEditorWindow))
            {
                continue;
            }

            if (await editorWindow.TryActivateSpineGroupDocumentTabAsync(bundleId, groupId))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryFindMainSpineGroupDocumentTab(
        BundleId bundleId,
        string groupId,
        out BundleDocumentTabViewModel bundleTab,
        out SpineAssetGroupDocumentTabViewModel spineGroupTab)
    {
        foreach (var candidateBundleTab in _owner.ViewModel.DocumentWorkspace.BundleTabs)
        {
            if (!candidateBundleTab.BundleId.Equals(bundleId) ||
                !candidateBundleTab.TryGetSpineGroupTab(groupId, out var candidateGroupTab))
            {
                continue;
            }

            bundleTab = candidateBundleTab;
            spineGroupTab = candidateGroupTab;
            return true;
        }

        bundleTab = null!;
        spineGroupTab = null!;
        return false;
    }

    private void SynchronizeMainWindowSelection(
        BundleDocumentTabViewModel bundleTab,
        AssetDocumentTabViewModel assetTab)
    {
        if (_owner.FindName("BundleTabView") is TabView bundleTabView &&
            !ReferenceEquals(bundleTabView.SelectedItem, bundleTab))
        {
            bundleTabView.SelectedItem = bundleTab;
        }

        if (_owner.FindName("AssetDocumentTabView") is TabView assetDocumentTabView &&
            bundleTab.DocumentTabs.FirstOrDefault(tab => tab.Wraps(assetTab)) is { } documentTab &&
            !ReferenceEquals(assetDocumentTabView.SelectedItem, documentTab))
        {
            assetDocumentTabView.SelectedItem = documentTab;
        }
    }

    private void SynchronizeMainWindowSelection(
        BundleDocumentTabViewModel bundleTab,
        SpineAssetGroupDocumentTabViewModel spineGroupTab)
    {
        if (_owner.FindName("BundleTabView") is TabView bundleTabView &&
            !ReferenceEquals(bundleTabView.SelectedItem, bundleTab))
        {
            bundleTabView.SelectedItem = bundleTab;
        }

        if (_owner.FindName("AssetDocumentTabView") is TabView assetDocumentTabView &&
            bundleTab.DocumentTabs.FirstOrDefault(tab => tab.Wraps(spineGroupTab)) is { } documentTab &&
            !ReferenceEquals(assetDocumentTabView.SelectedItem, documentTab))
        {
            assetDocumentTabView.SelectedItem = documentTab;
        }
    }

    private void TryBringMainWindowToFront()
    {
        var current = (DependencyObject?)_owner;
        while (current is not null)
        {
            if (current is ShellPage { HostWindow: { } hostWindow })
            {
                hostWindow.BringToFront();
                return;
            }

            current = VisualTreeHelper.GetParent(current);
        }
    }

    private static int? IndexOf(
        IReadOnlyList<AssetEditorWindow> editorWindows,
        AssetEditorWindow? editorWindow)
    {
        if (editorWindow is null)
        {
            return null;
        }

        for (var index = 0; index < editorWindows.Count; index++)
        {
            if (ReferenceEquals(editorWindows[index], editorWindow))
            {
                return index;
            }
        }

        return null;
    }
}
