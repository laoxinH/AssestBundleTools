using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class BundleExplorerViewModel
{
    internal WorkspaceWindowSizeDto? GetRememberedWorkspaceWindowSize(WorkspaceWindowSizeKind kind) =>
        _browserStateCoordinator.GetRememberedWindowSize(kind);

    internal void RememberWorkspaceWindowSize(
        WorkspaceWindowSizeKind kind,
        WorkspaceWindowSizeDto size)
    {
        if (_browserStateCoordinator.RememberWindowSize(kind, size))
        {
            RequestWorkspaceBrowserStatePersistence();
        }
    }
}
