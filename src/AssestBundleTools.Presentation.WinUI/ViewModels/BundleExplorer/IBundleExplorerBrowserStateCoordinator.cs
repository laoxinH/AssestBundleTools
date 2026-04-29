namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 为工作区绑定协调器暴露浏览状态恢复与持久化控制的最小能力。
/// </summary>
internal interface IBundleExplorerBrowserStateCoordinator
{
    Task<bool> TryRestoreWorkspaceBrowserStateAsync();

    void CancelWorkspaceBrowserStatePersistence();
}
