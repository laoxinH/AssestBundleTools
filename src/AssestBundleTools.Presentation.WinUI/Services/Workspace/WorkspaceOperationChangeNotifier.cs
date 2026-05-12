using System.Runtime.InteropServices;
using System.Threading;
using AssestBundleTools.Domain.Workspace;
using Microsoft.UI.Dispatching;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 轻量事件总线，仅用于提示页面级读模型需要重新读取。
/// </summary>
public sealed class WorkspaceOperationChangeNotifier : IWorkspaceOperationChangeNotifier
{
    private readonly DispatcherQueue? _dispatcherQueue = TryGetCurrentDispatcherQueue();
    private readonly AsyncLocal<NotificationBatchState?> _currentBatch = new();

    public event EventHandler<WorkspaceOperationChangedEventArgs>? WorkspaceOperationsChanged;

    public IDisposable BeginNotificationBatch()
    {
        var parent = _currentBatch.Value;
        var batch = new NotificationBatchState(parent);
        _currentBatch.Value = batch;
        return new NotificationBatchScope(this, batch);
    }

    public void NotifyWorkspaceOperationsChanged(
        WorkspaceId workspaceId,
        WorkspaceOperationChangeKind changeKind)
    {
        if (changeKind == WorkspaceOperationChangeKind.None)
        {
            return;
        }

        var currentBatch = _currentBatch.Value;
        if (currentBatch is not null)
        {
            currentBatch.Add(workspaceId, changeKind);
            return;
        }

        var eventArgs = new WorkspaceOperationChangedEventArgs(workspaceId, changeKind);
        Raise(eventArgs);
    }

    private void CompleteBatch(NotificationBatchState batch)
    {
        if (!ReferenceEquals(_currentBatch.Value, batch))
        {
            return;
        }

        _currentBatch.Value = batch.Parent;
        if (batch.Parent is not null)
        {
            batch.Parent.Merge(batch);
            return;
        }

        foreach (var eventArgs in batch.CreateEventArgs())
        {
            Raise(eventArgs);
        }
    }

    private void Raise(WorkspaceOperationChangedEventArgs eventArgs)
    {
        if (_dispatcherQueue is not null && !_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(() => WorkspaceOperationsChanged?.Invoke(this, eventArgs));
            return;
        }

        WorkspaceOperationsChanged?.Invoke(this, eventArgs);
    }

    private static DispatcherQueue? TryGetCurrentDispatcherQueue()
    {
        try
        {
            return DispatcherQueue.GetForCurrentThread();
        }
        catch (COMException)
        {
            return null;
        }
    }

    private sealed class NotificationBatchScope : IDisposable
    {
        private readonly WorkspaceOperationChangeNotifier _owner;
        private readonly NotificationBatchState _batch;
        private bool _isDisposed;

        public NotificationBatchScope(
            WorkspaceOperationChangeNotifier owner,
            NotificationBatchState batch)
        {
            _owner = owner;
            _batch = batch;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _owner.CompleteBatch(_batch);
        }
    }

    private sealed class NotificationBatchState
    {
        private readonly Dictionary<string, PendingNotification> _pending = new(StringComparer.Ordinal);

        public NotificationBatchState(NotificationBatchState? parent)
        {
            Parent = parent;
        }

        public NotificationBatchState? Parent { get; }

        public void Add(
            WorkspaceId workspaceId,
            WorkspaceOperationChangeKind changeKind)
        {
            if (changeKind == WorkspaceOperationChangeKind.None)
            {
                return;
            }

            var key = workspaceId.Value;
            if (_pending.TryGetValue(key, out var existing))
            {
                _pending[key] = existing with
                {
                    ChangeKind = existing.ChangeKind | changeKind
                };
                return;
            }

            _pending[key] = new PendingNotification(workspaceId, changeKind);
        }

        public void Merge(NotificationBatchState child)
        {
            foreach (var item in child._pending.Values)
            {
                Add(item.WorkspaceId, item.ChangeKind);
            }
        }

        public IEnumerable<WorkspaceOperationChangedEventArgs> CreateEventArgs()
        {
            foreach (var item in _pending.Values)
            {
                yield return new WorkspaceOperationChangedEventArgs(
                    item.WorkspaceId,
                    item.ChangeKind);
            }
        }
    }

    private sealed record PendingNotification(
        WorkspaceId WorkspaceId,
        WorkspaceOperationChangeKind ChangeKind);
}
