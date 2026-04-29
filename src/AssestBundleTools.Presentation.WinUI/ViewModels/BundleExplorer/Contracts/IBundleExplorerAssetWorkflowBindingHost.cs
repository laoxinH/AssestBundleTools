using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;

/// <summary>
/// 为工作区绑定协调器暴露资产工作流所需的最小展示宿主能力。
/// </summary>
internal interface IBundleExplorerAssetWorkflowBindingHost
{
    AssetPaneViewModel PrimaryPane { get; }

    void ClearActiveTabState();
}
