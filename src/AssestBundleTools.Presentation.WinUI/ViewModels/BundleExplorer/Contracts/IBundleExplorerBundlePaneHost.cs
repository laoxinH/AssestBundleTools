using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 向页面内其他协作对象暴露左侧资源包窗格的最小共享能力。
/// </summary>
public interface IBundleExplorerBundlePaneHost
{
    void SetSelectedBundle(BundleNodeViewModel? bundleNode);

    BundleNodeViewModel? FindBundleNodeByDisplayName(string displayName);

    string ResolveBundleDisplayName(BundleId bundleId);

    string ResolvePrimaryPaneEmptyStateText(
        string noWorkspaceText,
        string emptyWorkspaceText,
        string noOpenTabsText);
}
