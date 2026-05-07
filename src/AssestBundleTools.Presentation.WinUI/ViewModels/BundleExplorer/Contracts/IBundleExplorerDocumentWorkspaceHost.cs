using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;

/// <summary>
/// 向页面内其他协作对象暴露文档宿主的最小共享状态。
/// </summary>
public interface IBundleExplorerDocumentWorkspaceHost
{
    event EventHandler? AssetDocumentHostStateChanged;

    BundleDocumentTabViewModel? ActiveBundleTab { get; }

    AssetDocumentTabViewModel? ActiveAssetDocumentTab { get; }

    SpineAssetGroupDocumentTabViewModel? ActiveSpineGroupDocumentTab { get; }

    void SetActiveBundleTab(BundleDocumentTabViewModel? bundleTab);

    void NotifyAssetDocumentHostStateChanged();
}
