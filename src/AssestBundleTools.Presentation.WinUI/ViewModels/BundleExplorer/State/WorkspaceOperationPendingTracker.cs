using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.State;

public sealed class WorkspaceOperationPendingTracker
{
    private readonly Dictionary<string, int> _operationIds = new(StringComparer.Ordinal);

    public event EventHandler? PendingStateChanged;

    public IDisposable Begin(WorkspaceId workspaceId, string operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);

        var key = CreateKey(workspaceId, operationId);
        var added = !_operationIds.ContainsKey(key);
        _operationIds[key] = added ? 1 : _operationIds[key] + 1;
        if (added)
        {
            PendingStateChanged?.Invoke(this, EventArgs.Empty);
        }

        return new PendingScope(this, key);
    }

    public bool IsPending(WorkspaceId workspaceId, string? operationId)
    {
        return !string.IsNullOrWhiteSpace(operationId) &&
               _operationIds.ContainsKey(CreateKey(workspaceId, operationId));
    }

    private void End(string key)
    {
        if (!_operationIds.TryGetValue(key, out var count))
        {
            return;
        }

        if (count > 1)
        {
            _operationIds[key] = count - 1;
            return;
        }

        if (_operationIds.Remove(key))
        {
            PendingStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string CreateKey(WorkspaceId workspaceId, string operationId) =>
        string.Concat(workspaceId.Value, "\n", operationId.Trim());

    private sealed class PendingScope : IDisposable
    {
        private readonly WorkspaceOperationPendingTracker _tracker;
        private readonly string _key;
        private bool _isDisposed;

        public PendingScope(WorkspaceOperationPendingTracker tracker, string key)
        {
            _tracker = tracker;
            _key = key;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _tracker.End(_key);
        }
    }
}
