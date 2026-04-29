namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 为工作区绑定协调器暴露资产工作流所需的最小展示宿主能力。
/// </summary>
internal interface IBundleExplorerAssetWorkflowBindingHost
{
    AssetPaneViewModel PrimaryPane { get; }

    void ClearActiveTabState();
}
