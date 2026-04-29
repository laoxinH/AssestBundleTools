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
/// 表示左侧树中一个可打开的 Bundle 节点。
/// </summary>
public sealed class BundleNodeViewModel : ObservableObject
{
    private bool _isSelected;

    public BundleNodeViewModel(
        BundleId bundleId,
        string displayName,
        string logicalPath,
        string sourcePath,
        int assetCount)
    {
        BundleId = bundleId;
        DisplayName = displayName;
        LogicalPath = logicalPath;
        SourcePath = sourcePath;
        AssetCount = assetCount;
    }

    public BundleId BundleId { get; }

    public string DisplayName { get; }

    public string LogicalPath { get; }

    public string SourcePath { get; }

    public int AssetCount { get; }

    public bool CanOpenSourceLocation => !string.IsNullOrWhiteSpace(SourcePath);

    /// <summary>
    /// 左侧资源包项的选中态。
    /// 用于驱动与资产列表一致的浅色选中背景和左侧强调条。
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
