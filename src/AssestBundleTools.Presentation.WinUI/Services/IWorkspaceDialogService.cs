using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 封装工作区新建、打开方式选择和删除确认等 UI 交互。
/// </summary>
public interface IWorkspaceDialogService
{
    /// <summary>
    /// 以集中式可视化配置面板的方式收集“新建工作区”所需参数。
    /// </summary>
    Task<WorkspaceNavigationRequest?> RequestNewWorkspaceAsync(
        XamlRoot xamlRoot,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 以集中式可视化配置面板的方式收集“打开工作区”所需参数。
    /// </summary>
    Task<WorkspaceNavigationRequest?> RequestOpenWorkspaceFromDirectoryAsync(
        XamlRoot xamlRoot,
        CancellationToken cancellationToken = default);

    Task<WorkspaceOpenTarget?> RequestWorkspaceOpenTargetAsync(
        XamlRoot xamlRoot,
        string workspaceDisplayName,
        CancellationToken cancellationToken = default);

    Task<bool> ConfirmDeleteWorkspaceAsync(
        XamlRoot xamlRoot,
        string workspaceDisplayName,
        string workspaceDataRootPath,
        CancellationToken cancellationToken = default);
}
