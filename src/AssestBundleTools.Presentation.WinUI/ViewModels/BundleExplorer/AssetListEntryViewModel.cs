using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 表示资产列表中用于展示的一行数据。
/// </summary>
/// <remarks>
/// 这里保留原始 DTO，方便页面继续围绕“列表展示 + 检查器详情 + 标签页状态恢复”组织交互，
/// 同时把界面需要的格式化文本集中放在 UI 层，避免把展示逻辑回流到应用层。
/// </remarks>
public sealed class AssetListEntryViewModel : ObservableObject
{
    private bool _isSelected;

    public AssetListEntryViewModel(
        AssetListItemDto item,
        string assetTypeText,
        string sizeText,
        string stateText)
    {
        Item = item;
        AssetTypeText = assetTypeText;
        SizeText = sizeText;
        StateText = stateText;
    }

    public AssetListItemDto Item { get; }

    public string AssetTypeText { get; }

    public string SizeText { get; }

    public string StateText { get; }

    /// <summary>
    /// 统一由 UI 层提供名称展示文本，避免 XAML 直接深入 DTO 结构绑定。
    /// </summary>
    public string DisplayName => Item.Name;

    /// <summary>
    /// 资源路径继续作为名称列的副标题显示，便于区分同名资产。
    /// </summary>
    public string DisplayAssetPath => Item.AssetPath;

    /// <summary>
    /// 完整名称提示同样收敛在行 ViewModel，减少 XAML 侧重复绑定和告警噪音。
    /// </summary>
    public string DisplayNameToolTipText => DisplayName;

    /// <summary>
    /// 行级选中态由窗格 ViewModel 统一维护，
    /// 这样资产列表可以稳定复用“浅背景 + 左侧强调条”的选中语言。
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(SelectionIndicatorVisibility));
                OnPropertyChanged(nameof(SelectionBackgroundOpacity));
            }
        }
    }

    public Visibility SelectionIndicatorVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;

    public double SelectionBackgroundOpacity => IsSelected ? 1d : 0d;
}
