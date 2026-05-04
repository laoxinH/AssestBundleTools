using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示资源依赖列表中的展示项。
/// </summary>
public sealed record AssetDependencyViewModel(
    string Name,
    string BundleName,
    string MetadataText,
    string SupplementaryText,
    ResolvedAssetNavigationTarget? Target = null,
    string OpenButtonText = "",
    string OpenButtonToolTipText = "")
{
    public Visibility SupplementaryVisibility =>
        string.IsNullOrWhiteSpace(SupplementaryText)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility OpenButtonVisibility =>
        Target is null
            ? Visibility.Collapsed
            : Visibility.Visible;
}
