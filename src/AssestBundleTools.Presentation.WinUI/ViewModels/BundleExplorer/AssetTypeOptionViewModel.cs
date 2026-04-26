using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 表示资产类型过滤下拉框中的一个可选项。
/// </summary>
public sealed record AssetTypeOptionViewModel(
    string DisplayName,
    AssetType? AssetType);
