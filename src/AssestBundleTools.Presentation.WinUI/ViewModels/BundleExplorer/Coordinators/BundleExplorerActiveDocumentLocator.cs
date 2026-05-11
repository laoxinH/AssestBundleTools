using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 负责把右侧当前文档定位回中间资源列表焦点。
/// </summary>
internal sealed class BundleExplorerActiveDocumentLocator
{
    private readonly IBundleExplorerDocumentWorkspaceHost _documentWorkspace;
    private readonly AssetPaneViewModel _primaryPane;
    private readonly Func<bool, Task> _refreshPrimaryPaneAsync;
    private readonly Action _captureActiveTabState;

    public BundleExplorerActiveDocumentLocator(
        IBundleExplorerDocumentWorkspaceHost documentWorkspace,
        AssetPaneViewModel primaryPane,
        Func<bool, Task> refreshPrimaryPaneAsync,
        Action captureActiveTabState)
    {
        _documentWorkspace = documentWorkspace ?? throw new ArgumentNullException(nameof(documentWorkspace));
        _primaryPane = primaryPane ?? throw new ArgumentNullException(nameof(primaryPane));
        _refreshPrimaryPaneAsync = refreshPrimaryPaneAsync ?? throw new ArgumentNullException(nameof(refreshPrimaryPaneAsync));
        _captureActiveTabState = captureActiveTabState ?? throw new ArgumentNullException(nameof(captureActiveTabState));
    }

    public async Task<bool> LocateActiveDocumentAsync()
    {
        var activeDocument = _documentWorkspace.ActiveBundleTab?.ActiveDocumentTab;
        if (activeDocument?.AssetTab is { } assetTab)
        {
            await EnsureViewModeAsync(AssetPaneViewMode.Assets);
            var selected = _primaryPane.TrySelectAsset(assetTab.AssetId);
            if (selected)
            {
                _captureActiveTabState();
            }

            return selected;
        }

        if (activeDocument?.SpineGroupTab is { } spineGroupTab)
        {
            await EnsureViewModeAsync(AssetPaneViewMode.SpineGroups);
            var selected = _primaryPane.TrySelectSpineGroup(spineGroupTab.GroupId);
            if (selected)
            {
                _captureActiveTabState();
            }

            return selected;
        }

        return false;
    }

    private async Task EnsureViewModeAsync(AssetPaneViewMode viewMode)
    {
        if (_primaryPane.ViewMode == viewMode)
        {
            return;
        }

        _primaryPane.ViewMode = viewMode;
        await _refreshPrimaryPaneAsync(true);
    }
}
