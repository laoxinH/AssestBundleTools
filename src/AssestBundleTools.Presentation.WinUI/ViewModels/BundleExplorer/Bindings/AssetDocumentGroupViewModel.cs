using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示一个可独立承载资产文档标签的页面级标签组。
/// </summary>
/// <remarks>
/// 该类型只维护窗口/标签宿主状态，不持有工作区事实数据。
/// 主窗口右侧和独立资源编辑窗口后续都可以向这个模型逐步收口。
/// </remarks>
public sealed class AssetDocumentGroupViewModel : ObservableObject
{
    private AssetDocumentTabViewModel? _activeAssetTab;

    public ObservableCollection<AssetDocumentTabViewModel> AssetTabs { get; } = new();

    public AssetDocumentTabViewModel? ActiveAssetTab
    {
        get => _activeAssetTab;
        private set
        {
            if (SetProperty(ref _activeAssetTab, value))
            {
                NotifyGroupStateChanged();
            }
        }
    }

    public bool HasAssetTabs => AssetTabs.Count > 0;

    public Visibility TabStripVisibility => HasAssetTabs ? Visibility.Visible : Visibility.Collapsed;

    public bool TryGetAssetTab(
        BundleId bundleId,
        AssetId assetId,
        [NotNullWhen(true)] out AssetDocumentTabViewModel? assetTab)
    {
        assetTab = AssetTabs.FirstOrDefault(tab =>
            tab.BundleId.Equals(bundleId) &&
            tab.AssetId.Equals(assetId));
        return assetTab is not null;
    }

    public void AddAssetTab(AssetDocumentTabViewModel assetTab, bool activate)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        if (!AssetTabs.Contains(assetTab))
        {
            AssetTabs.Add(assetTab);
            NotifyGroupStateChanged();
        }

        if (activate)
        {
            SetActiveAssetTab(assetTab);
        }
    }

    public void AddAssetTabs(AssetDocumentGroupTransfer transfer)
    {
        ArgumentNullException.ThrowIfNull(transfer);

        AssetDocumentTabViewModel? activeAssetTab = null;
        foreach (var assetTab in transfer.AssetTabs)
        {
            if (TryGetAssetTab(assetTab.BundleId, assetTab.AssetId, out var existingAssetTab))
            {
                assetTab.CancelPreviewRequest();
                if (ReferenceEquals(assetTab, transfer.ActiveAssetTab))
                {
                    activeAssetTab = existingAssetTab;
                }

                continue;
            }

            AssetTabs.Add(assetTab);
            if (ReferenceEquals(assetTab, transfer.ActiveAssetTab))
            {
                activeAssetTab = assetTab;
            }
        }

        NotifyGroupStateChanged();
        SetActiveAssetTab(activeAssetTab ?? transfer.ActiveAssetTab);
    }

    public void SetActiveAssetTab(AssetDocumentTabViewModel? assetTab)
    {
        if (assetTab is not null && !AssetTabs.Contains(assetTab))
        {
            AssetTabs.Add(assetTab);
        }

        var previousActiveAssetTab = ActiveAssetTab;
        ActiveAssetTab = assetTab;
        if (!ReferenceEquals(previousActiveAssetTab, ActiveAssetTab))
        {
            previousActiveAssetTab?.CancelPreviewRequest();
        }
    }

    public bool ReorderAssetTabs(IReadOnlyList<AssetDocumentTabViewModel> orderedAssetTabs)
    {
        ArgumentNullException.ThrowIfNull(orderedAssetTabs);

        if (!ObservableCollectionOrderHelper.Reorder(AssetTabs, orderedAssetTabs))
        {
            return false;
        }

        NotifyGroupStateChanged();
        return true;
    }

    public void RemoveAssetTab(AssetDocumentTabViewModel assetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        if (!AssetTabs.Remove(assetTab))
        {
            return;
        }

        assetTab.CancelPreviewRequest();
        if (ReferenceEquals(ActiveAssetTab, assetTab))
        {
            ActiveAssetTab = AssetTabs.FirstOrDefault();
        }

        NotifyGroupStateChanged();
    }

    /// <summary>
    /// 将标签移交给另一个宿主时不取消它自己的预览请求，
    /// 避免从主窗口移动到资源编辑窗口时丢掉已加载或正在加载的预览状态。
    /// </summary>
    public bool DetachAssetTab(AssetDocumentTabViewModel assetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        if (!AssetTabs.Remove(assetTab))
        {
            return false;
        }

        if (ReferenceEquals(ActiveAssetTab, assetTab))
        {
            ActiveAssetTab = AssetTabs.FirstOrDefault();
        }

        NotifyGroupStateChanged();
        return true;
    }

    public AssetDocumentGroupTransfer? DetachAssetTabs()
    {
        if (AssetTabs.Count == 0)
        {
            return null;
        }

        var transfer = new AssetDocumentGroupTransfer(AssetTabs.ToArray(), ActiveAssetTab);
        AssetTabs.Clear();
        ActiveAssetTab = null;
        NotifyGroupStateChanged();
        return transfer;
    }

    public void Clear()
    {
        foreach (var assetTab in AssetTabs)
        {
            assetTab.CancelPreviewRequest();
        }

        AssetTabs.Clear();
        ActiveAssetTab = null;
        NotifyGroupStateChanged();
    }

    private void NotifyGroupStateChanged()
    {
        OnPropertyChanged(nameof(HasAssetTabs));
        OnPropertyChanged(nameof(TabStripVisibility));
    }
}
