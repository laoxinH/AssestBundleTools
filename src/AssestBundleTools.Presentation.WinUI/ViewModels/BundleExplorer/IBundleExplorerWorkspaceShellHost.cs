using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 向页面内其他协作对象暴露工作区壳层的最小共享状态。
/// </summary>
public interface IBundleExplorerWorkspaceShellHost
{
    WorkspaceSummaryDto? WorkspaceSummary { get; }

    WorkspaceNavigationRequest? ActiveWorkspaceNavigationRequest { get; }
}
