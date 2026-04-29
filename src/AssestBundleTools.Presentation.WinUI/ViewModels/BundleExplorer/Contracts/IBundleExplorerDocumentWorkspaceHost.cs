namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 向页面内其他协作对象暴露文档宿主的最小共享状态。
/// </summary>
public interface IBundleExplorerDocumentWorkspaceHost
{
    event EventHandler? AssetDocumentHostStateChanged;

    BundleDocumentTabViewModel? ActiveBundleTab { get; }

    AssetDocumentTabViewModel? ActiveAssetDocumentTab { get; }

    void SetActiveBundleTab(BundleDocumentTabViewModel? bundleTab);

    void NotifyAssetDocumentHostStateChanged();
}
