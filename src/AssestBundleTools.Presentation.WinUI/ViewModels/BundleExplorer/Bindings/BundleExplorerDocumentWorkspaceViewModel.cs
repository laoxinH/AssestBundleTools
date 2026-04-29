using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 维护工作台中“资源包标签 + 资产详情标签”的页面级浏览状态。
/// </summary>
/// <remarks>
/// 这个子 ViewModel 只负责文档宿主的集合、激活项和重排状态。
/// 真正的资源加载、预览生成和替换流程仍由外层编排 ViewModel 统一协调。
/// </remarks>
public sealed class BundleExplorerDocumentWorkspaceViewModel : ObservableObject, IBundleExplorerDocumentWorkspaceHost
{
    private readonly ObservableCollection<AssetDocumentTabViewModel> _emptyAssetTabs = [];
    private BundleDocumentTabViewModel? _activeBundleTab;
    private int _assetDocumentHostSynchronizationDepth;

    public event EventHandler? AssetDocumentHostStateChanged;

    internal event EventHandler<BundleExplorerPageStateChangedEventArgs<BundleExplorerDocumentWorkspacePageStateChange>>? PageStateChanged;

    public ObservableCollection<BundleDocumentTabViewModel> BundleTabs { get; } = new();

    public ObservableCollection<AssetDocumentTabViewModel> CurrentAssetTabs =>
        ActiveBundleTab?.AssetTabs ?? _emptyAssetTabs;

    public AssetDocumentTabViewModel? ActiveAssetDocumentTab => ActiveBundleTab?.ActiveAssetTab;

    public bool HasAssetDocumentTabs => CurrentAssetTabs.Count > 0;

    public Visibility AssetDocumentTabStripVisibility =>
        HasAssetDocumentTabs ? Visibility.Visible : Visibility.Collapsed;

    public bool HasOpenTabs => ActiveBundleTab is not null;

    /// <summary>
    /// 右侧资产文档标签宿主在切换资源包标签时，会经历一次 ItemsSource 重绑。
    /// 这段时间内需要抑制控件把默认选中反向写回状态。
    /// </summary>
    public bool IsSynchronizingAssetDocumentHostSelection => _assetDocumentHostSynchronizationDepth > 0;

    public BundleDocumentTabViewModel? ActiveBundleTab
    {
        get => _activeBundleTab;
        private set
        {
            if (SetProperty(ref _activeBundleTab, value))
            {
                OnPropertyChanged(nameof(HasOpenTabs));
                NotifyAssetDocumentHostStateChanged();
                NotifyPageStateChanged(BundleExplorerDocumentWorkspacePageStateChange.ActiveBundleContext);
            }
        }
    }

    public bool ReorderBundleTabs(IReadOnlyList<BundleDocumentTabViewModel> orderedTabs)
    {
        ArgumentNullException.ThrowIfNull(orderedTabs);
        return ObservableCollectionOrderHelper.Reorder(BundleTabs, orderedTabs);
    }

    public bool ReorderActiveAssetTabs(IReadOnlyList<AssetDocumentTabViewModel> orderedTabs)
    {
        ArgumentNullException.ThrowIfNull(orderedTabs);
        return ActiveBundleTab is not null && ActiveBundleTab.ReorderAssetTabs(orderedTabs);
    }

    /// <summary>
    /// 统一处理资源包标签页打开策略：
    /// 已打开则复用；显式固定打开则新建固定标签；否则优先复用预览标签。
    /// </summary>
    public BundleDocumentTabViewModel OpenBundle(BundleNodeViewModel bundleNode, bool openAsPinnedTab)
    {
        ArgumentNullException.ThrowIfNull(bundleNode);

        if (TryGetBundleTab(bundleNode.BundleId, out var existingTab))
        {
            if (openAsPinnedTab)
            {
                existingTab.PromoteToPinnedTab();
            }

            return existingTab;
        }

        if (openAsPinnedTab)
        {
            var pinnedTab = CreateBundleTab(bundleNode, isPreview: false);
            BundleTabs.Add(pinnedTab);
            return pinnedTab;
        }

        if (ActiveBundleTab is { IsPreview: true } previewTab)
        {
            previewTab.RebindBundle(
                bundleNode.BundleId,
                bundleNode.DisplayName,
                bundleNode.LogicalPath,
                isPreview: true);
            return previewTab;
        }

        var newPreviewTab = CreateBundleTab(bundleNode, isPreview: true);
        BundleTabs.Add(newPreviewTab);
        return newPreviewTab;
    }

    public bool TryGetBundleTab(BundleId bundleId, [NotNullWhen(true)] out BundleDocumentTabViewModel? bundleTab)
    {
        bundleTab = BundleTabs.FirstOrDefault(tab => tab.BundleId.Equals(bundleId));
        return bundleTab is not null;
    }

    public void SetActiveBundleTab(BundleDocumentTabViewModel? bundleTab)
    {
        ActiveBundleTab = bundleTab;
    }

    public void BeginAssetDocumentHostSynchronization()
    {
        _assetDocumentHostSynchronizationDepth++;
        OnPropertyChanged(nameof(IsSynchronizingAssetDocumentHostSelection));
        NotifyPageStateChanged(BundleExplorerDocumentWorkspacePageStateChange.HostSynchronization);
    }

    public void EndAssetDocumentHostSynchronization()
    {
        if (_assetDocumentHostSynchronizationDepth <= 0)
        {
            _assetDocumentHostSynchronizationDepth = 0;
            OnPropertyChanged(nameof(IsSynchronizingAssetDocumentHostSelection));
            NotifyPageStateChanged(BundleExplorerDocumentWorkspacePageStateChange.HostSynchronization);
            return;
        }

        _assetDocumentHostSynchronizationDepth--;
        OnPropertyChanged(nameof(IsSynchronizingAssetDocumentHostSelection));
        NotifyPageStateChanged(BundleExplorerDocumentWorkspacePageStateChange.HostSynchronization);
    }

    public void Clear()
    {
        BundleTabs.Clear();
        ActiveBundleTab = null;
    }

    /// <summary>
    /// 当前活动资源包下的资产文档集合发生变化后，统一刷新派生状态。
    /// </summary>
    public void NotifyAssetDocumentHostStateChanged()
    {
        OnPropertyChanged(nameof(CurrentAssetTabs));
        OnPropertyChanged(nameof(ActiveAssetDocumentTab));
        OnPropertyChanged(nameof(HasAssetDocumentTabs));
        OnPropertyChanged(nameof(AssetDocumentTabStripVisibility));
        AssetDocumentHostStateChanged?.Invoke(this, EventArgs.Empty);
        NotifyPageStateChanged(BundleExplorerDocumentWorkspacePageStateChange.AssetDocumentHost);
    }

    private static BundleDocumentTabViewModel CreateBundleTab(BundleNodeViewModel bundleNode, bool isPreview) =>
        new(
            bundleNode.BundleId,
            bundleNode.DisplayName,
            bundleNode.LogicalPath,
            isPreview);

    private void NotifyPageStateChanged(BundleExplorerDocumentWorkspacePageStateChange changeFlags)
    {
        if (changeFlags == BundleExplorerDocumentWorkspacePageStateChange.None)
        {
            return;
        }

        PageStateChanged?.Invoke(this, new BundleExplorerPageStateChangedEventArgs<BundleExplorerDocumentWorkspacePageStateChange>(changeFlags));
    }
}
