using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 表示资源依赖列表中的展示项。
/// </summary>
public sealed record AssetDependencyViewModel(
    string Name,
    string BundleName,
    string MetadataText,
    string SupplementaryText)
{
    public Visibility SupplementaryVisibility =>
        string.IsNullOrWhiteSpace(SupplementaryText)
            ? Visibility.Collapsed
            : Visibility.Visible;
}
