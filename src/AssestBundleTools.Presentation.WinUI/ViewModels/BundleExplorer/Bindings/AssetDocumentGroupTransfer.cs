namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 资产文档组在不同宿主之间移动时携带的页面级标签状态。
/// </summary>
public sealed class AssetDocumentGroupTransfer
{
    public AssetDocumentGroupTransfer(
        IReadOnlyList<AssetDocumentTabViewModel> assetTabs,
        AssetDocumentTabViewModel? activeAssetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTabs);
        if (assetTabs.Count == 0)
        {
            throw new ArgumentException("The asset document group must contain at least one tab.", nameof(assetTabs));
        }

        AssetTabs = assetTabs;
        ActiveAssetTab = activeAssetTab is not null && assetTabs.Contains(activeAssetTab)
            ? activeAssetTab
            : assetTabs[0];
    }

    public IReadOnlyList<AssetDocumentTabViewModel> AssetTabs { get; }

    public AssetDocumentTabViewModel ActiveAssetTab { get; }
}
