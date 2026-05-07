using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;

/// <summary>
/// 统一处理工作台资源包导入入口与加载遮罩进度。
/// </summary>
internal sealed class BundleExplorerWorkspaceImportActionHandler
{
    private readonly IWorkspaceImportCoordinatorService _workspaceImportCoordinatorService;
    private readonly WorkspaceImportProgressViewModel _workspaceImportProgress;
    private readonly BundleExplorerWorkspaceBindingCoordinator _workspaceBindingCoordinator;
    private readonly Func<bool> _hasWorkspaceProvider;
    private readonly Func<bool> _isBusyProvider;
    private readonly Action<bool> _setBusyState;

    public BundleExplorerWorkspaceImportActionHandler(
        IWorkspaceImportCoordinatorService workspaceImportCoordinatorService,
        WorkspaceImportProgressViewModel workspaceImportProgress,
        BundleExplorerWorkspaceBindingCoordinator workspaceBindingCoordinator,
        Func<bool> hasWorkspaceProvider,
        Func<bool> isBusyProvider,
        Action<bool> setBusyState)
    {
        _workspaceImportCoordinatorService = workspaceImportCoordinatorService ?? throw new ArgumentNullException(nameof(workspaceImportCoordinatorService));
        _workspaceImportProgress = workspaceImportProgress ?? throw new ArgumentNullException(nameof(workspaceImportProgress));
        _workspaceBindingCoordinator = workspaceBindingCoordinator ?? throw new ArgumentNullException(nameof(workspaceBindingCoordinator));
        _hasWorkspaceProvider = hasWorkspaceProvider ?? throw new ArgumentNullException(nameof(hasWorkspaceProvider));
        _isBusyProvider = isBusyProvider ?? throw new ArgumentNullException(nameof(isBusyProvider));
        _setBusyState = setBusyState ?? throw new ArgumentNullException(nameof(setBusyState));
    }

    public Task RequestBundleFileImportAsync(CancellationToken cancellationToken = default) =>
        RunGuardedWorkspaceImportAsync(
            importAsync: (progress, token) => _workspaceImportCoordinatorService.RequestBundleFileImportAsync(progress, token),
            cancellationToken);

    public Task RequestBundleDirectoryImportAsync(CancellationToken cancellationToken = default) =>
        RunGuardedWorkspaceImportAsync(
            importAsync: (progress, token) => _workspaceImportCoordinatorService.RequestBundleDirectoryImportAsync(progress, token),
            cancellationToken);

    public Task RequestDragDropImportAsync(
        IReadOnlyList<string> droppedPaths,
        CancellationToken cancellationToken = default) =>
        RunGuardedWorkspaceImportAsync(
            importAsync: (progress, token) => _workspaceImportCoordinatorService.RequestDragDropImportAsync(droppedPaths, progress, token),
            cancellationToken);

    public async Task LoadWorkspaceWithImportOverlayAsync(
        bool clearExistingLogs,
        WorkspaceNavigationRequest workspaceNavigationRequest)
    {
        _workspaceImportProgress.Start();
        try
        {
            var progress = _workspaceImportProgress.CreateProgressReporter();
            await _workspaceBindingCoordinator.LoadWorkspaceAsync(
                clearExistingLogs,
                workspaceNavigationRequest,
                progress);
        }
        finally
        {
            _workspaceImportProgress.Complete();
        }
    }

    private async Task RunGuardedWorkspaceImportAsync(
        Func<IProgress<WorkspaceImportProgressDto>, CancellationToken, Task<bool>> importAsync,
        CancellationToken cancellationToken)
    {
        if (!_hasWorkspaceProvider() || _isBusyProvider())
        {
            return;
        }

        _setBusyState(true);
        _workspaceImportProgress.Start();
        try
        {
            var progress = _workspaceImportProgress.CreateProgressReporter();
            if (await importAsync(progress, cancellationToken))
            {
                await _workspaceBindingCoordinator.ReloadCurrentWorkspaceAsync(
                    showLoadedNotification: false,
                    allowBusyReentry: true);
            }
        }
        finally
        {
            _workspaceImportProgress.Complete();
            _setBusyState(false);
        }
    }
}
