using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 将工作区操作通知合并为页面读模型刷新请求，避免每个操作入口手动刷新页面。
/// </summary>
internal sealed class WorkspaceOperationChangeRefreshCoordinator : IDisposable
{
    private readonly IWorkspaceOperationChangeNotifier _operationChangeNotifier;
    private readonly Func<WorkspaceId?> _currentWorkspaceIdProvider;
    private readonly Func<bool> _isBusyProvider;
    private readonly Func<WorkspaceOperationChangedEventArgs, Task> _refreshAsync;
    private WorkspaceOperationChangedEventArgs? _pendingRefreshEventArgs;
    private bool _isRefreshLoopActive;
    private bool _isDisposed;

    public WorkspaceOperationChangeRefreshCoordinator(
        IWorkspaceOperationChangeNotifier operationChangeNotifier,
        Func<WorkspaceId?> currentWorkspaceIdProvider,
        Func<bool> isBusyProvider,
        Func<Task> refreshAsync)
        : this(
            operationChangeNotifier,
            currentWorkspaceIdProvider,
            isBusyProvider,
            _ => refreshAsync())
    {
        ArgumentNullException.ThrowIfNull(refreshAsync);
    }

    public WorkspaceOperationChangeRefreshCoordinator(
        IWorkspaceOperationChangeNotifier operationChangeNotifier,
        Func<WorkspaceId?> currentWorkspaceIdProvider,
        Func<bool> isBusyProvider,
        Func<WorkspaceOperationChangedEventArgs, Task> refreshAsync)
    {
        _operationChangeNotifier = operationChangeNotifier ?? throw new ArgumentNullException(nameof(operationChangeNotifier));
        _currentWorkspaceIdProvider = currentWorkspaceIdProvider ?? throw new ArgumentNullException(nameof(currentWorkspaceIdProvider));
        _isBusyProvider = isBusyProvider ?? throw new ArgumentNullException(nameof(isBusyProvider));
        _refreshAsync = refreshAsync ?? throw new ArgumentNullException(nameof(refreshAsync));
        _operationChangeNotifier.WorkspaceOperationsChanged += OnWorkspaceOperationsChanged;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _operationChangeNotifier.WorkspaceOperationsChanged -= OnWorkspaceOperationsChanged;
        _isDisposed = true;
    }

    public void HandleBusyStateChanged(bool isBusy)
    {
        if (!isBusy && _pendingRefreshEventArgs is { } pendingRefreshEventArgs)
        {
            _ = RefreshAsync(pendingRefreshEventArgs);
        }
    }

    private async void OnWorkspaceOperationsChanged(
        object? sender,
        WorkspaceOperationChangedEventArgs eventArgs)
    {
        if (_isDisposed || !MatchesCurrentWorkspace(eventArgs.WorkspaceId))
        {
            return;
        }

        await RefreshAsync(eventArgs);
    }

    private async Task RefreshAsync(WorkspaceOperationChangedEventArgs eventArgs)
    {
        if (_isDisposed || _isBusyProvider() || _isRefreshLoopActive)
        {
            QueuePendingRefresh(eventArgs);
            return;
        }

        _isRefreshLoopActive = true;
        try
        {
            var nextEventArgs = eventArgs;
            do
            {
                _pendingRefreshEventArgs = null;
                await _refreshAsync(nextEventArgs);
                nextEventArgs = _pendingRefreshEventArgs!;
            }
            while (!_isDisposed && nextEventArgs is not null);
        }
        finally
        {
            _isRefreshLoopActive = false;
        }
    }

    private void QueuePendingRefresh(WorkspaceOperationChangedEventArgs eventArgs)
    {
        _pendingRefreshEventArgs = _pendingRefreshEventArgs is null
            ? eventArgs
            : _pendingRefreshEventArgs.Merge(eventArgs);
    }

    private bool MatchesCurrentWorkspace(WorkspaceId changedWorkspaceId)
    {
        var currentWorkspaceId = _currentWorkspaceIdProvider();
        return currentWorkspaceId.HasValue &&
               string.Equals(
                   currentWorkspaceId.Value.Value,
                   changedWorkspaceId.Value,
                   StringComparison.Ordinal);
    }
}
