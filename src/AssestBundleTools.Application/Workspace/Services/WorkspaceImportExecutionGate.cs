using System.Collections.Concurrent;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 基于 <see cref="SemaphoreSlim"/> 对同一工作区的导入处理和来源变更做串行化保护。
/// </summary>
public sealed class WorkspaceImportExecutionGate : IWorkspaceImportExecutionGate
{
    private readonly ConcurrentDictionary<string, GateEntry> _entries = new(StringComparer.Ordinal);
    private readonly object _entriesLock = new();

    public async Task<TResult> ExecuteExclusiveAsync<TResult>(
        string workspaceKey,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentNullException.ThrowIfNull(operation);

        var normalizedWorkspaceKey = workspaceKey.Trim();
        GateEntry entry;
        lock (_entriesLock)
        {
            entry = _entries.GetOrAdd(normalizedWorkspaceKey, static _ => new GateEntry());
            entry.AddReference();
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken);
            try
            {
                return await operation(cancellationToken);
            }
            finally
            {
                entry.Semaphore.Release();
            }
        }
        finally
        {
            var shouldDisposeEntry = false;
            lock (_entriesLock)
            {
                shouldDisposeEntry = entry.ReleaseReference() == 0 &&
                                     entry.Semaphore.CurrentCount == 1 &&
                                     _entries.TryRemove(new KeyValuePair<string, GateEntry>(normalizedWorkspaceKey, entry));
            }

            if (shouldDisposeEntry)
            {
                entry.Dispose();
            }
        }
    }

    private sealed class GateEntry : IDisposable
    {
        private int _referenceCount;

        public GateEntry()
        {
            Semaphore = new SemaphoreSlim(1, 1);
        }

        public SemaphoreSlim Semaphore { get; }

        public void AddReference()
        {
            Interlocked.Increment(ref _referenceCount);
        }

        public int ReleaseReference()
        {
            return Interlocked.Decrement(ref _referenceCount);
        }

        public void Dispose()
        {
            Semaphore.Dispose();
        }
    }
}
