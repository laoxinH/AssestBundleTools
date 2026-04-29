using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.Services;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;

/// <summary>
/// 向页面内其他协作对象暴露工作区壳层的最小共享状态。
/// </summary>
public interface IBundleExplorerWorkspaceShellHost
{
    WorkspaceSummaryDto? WorkspaceSummary { get; }

    WorkspaceNavigationRequest? ActiveWorkspaceNavigationRequest { get; }
}
