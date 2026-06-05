using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 合并 Spine 编辑态和操作通知带来的连续预览重载请求，保证同一组文档只执行最新一次刷新。
/// </summary>
internal sealed class SpineAssetGroupDetailsRefreshScheduler : IDisposable
{
    private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(350);

    private readonly object _gate = new();
    private readonly Dictionary<SpineAssetGroupDocumentTabViewModel, RefreshEntry> _entries = new();
    private readonly Func<SpineAssetGroupDetailsRefreshRequest, Task> _refreshAsync;
    private readonly Func<SpineAssetGroupDocumentTabViewModel, string> _signatureFactory;
    private readonly TimeSpan _debounceDelay;
    private bool _isDisposed;

    public SpineAssetGroupDetailsRefreshScheduler(
        Func<SpineAssetGroupDetailsRefreshRequest, Task> refreshAsync,
        TimeSpan? debounceDelay = null,
        Func<SpineAssetGroupDocumentTabViewModel, string>? signatureFactory = null)
    {
        _refreshAsync = refreshAsync ?? throw new ArgumentNullException(nameof(refreshAsync));
        _debounceDelay = debounceDelay ?? DefaultDebounceDelay;
        _signatureFactory = signatureFactory ?? SpineAssetGroupPreviewInputSignature.Create;
    }

    public Task RequestAsync(SpineAssetGroupDocumentTabViewModel groupTab)
    {
        ArgumentNullException.ThrowIfNull(groupTab);

        RefreshEntry entry;
        CancellationTokenSource? delayCancellationSource;
        CancellationTokenSource? runningCancellationSource;
        var shouldStartWorker = false;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            if (_isDisposed)
            {
                completion.SetResult();
                return completion.Task;
            }

            if (!_entries.TryGetValue(groupTab, out entry!))
            {
                entry = new RefreshEntry();
                _entries[groupTab] = entry;
            }

            var signature = _signatureFactory(groupTab);
            if (entry.IsWorkerActive &&
                string.Equals(entry.LatestSignature, signature, StringComparison.Ordinal))
            {
                entry.PendingCompletions.Add(completion);
                return completion.Task;
            }

            entry.Revision++;
            entry.LatestSignature = signature;
            entry.HasPendingRequest = true;
            entry.PendingCompletions.Add(completion);
            delayCancellationSource = entry.DelayCancellationSource;
            runningCancellationSource = entry.RunningCancellationSource;

            if (!entry.IsWorkerActive)
            {
                entry.IsWorkerActive = true;
                shouldStartWorker = true;
            }
        }

        TryCancel(delayCancellationSource);
        TryCancel(runningCancellationSource);
        if (shouldStartWorker)
        {
            _ = RunWorkerAsync(groupTab, entry);
        }

        return completion.Task;
    }

    public void Cancel(SpineAssetGroupDocumentTabViewModel groupTab)
    {
        ArgumentNullException.ThrowIfNull(groupTab);

        TaskCompletionSource[] completions;
        CancellationTokenSource? delayCancellationSource;
        CancellationTokenSource? runningCancellationSource;
        lock (_gate)
        {
            if (!_entries.Remove(groupTab, out var entry))
            {
                return;
            }

            entry.IsCancelled = true;
            delayCancellationSource = entry.DelayCancellationSource;
            runningCancellationSource = entry.RunningCancellationSource;
            completions = entry.TakeCompletions();
        }

        TryCancel(delayCancellationSource);
        TryCancel(runningCancellationSource);
        Complete(completions);
    }

    public void Dispose()
    {
        KeyValuePair<SpineAssetGroupDocumentTabViewModel, RefreshEntry>[] entries;
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            entries = _entries.ToArray();
            _entries.Clear();
        }

        foreach (var entry in entries)
        {
            entry.Value.IsCancelled = true;
            TryCancel(entry.Value.DelayCancellationSource);
            TryCancel(entry.Value.RunningCancellationSource);
            Complete(entry.Value.TakeCompletions());
        }
    }

    private async Task RunWorkerAsync(
        SpineAssetGroupDocumentTabViewModel groupTab,
        RefreshEntry entry)
    {
        while (true)
        {
            long revision;
            string signature;
            CancellationTokenSource delayCancellationSource;
            lock (_gate)
            {
                if (!TryPrepareNextDelay(groupTab, entry, out revision, out signature, out delayCancellationSource))
                {
                    return;
                }
            }

            try
            {
                await Task.Delay(_debounceDelay, delayCancellationSource.Token);
            }
            catch (OperationCanceledException) when (delayCancellationSource.IsCancellationRequested)
            {
                delayCancellationSource.Dispose();
                ClearDelayCancellationSource(entry, delayCancellationSource);
                continue;
            }

            delayCancellationSource.Dispose();
            ClearDelayCancellationSource(entry, delayCancellationSource);
            CancellationTokenSource runningCancellationSource;
            lock (_gate)
            {
                if (entry.IsCancelled || _isDisposed)
                {
                    StopEntry(groupTab, entry);
                    return;
                }

                if (entry.HasPendingRequest)
                {
                    continue;
                }

                runningCancellationSource = new CancellationTokenSource();
                entry.RunningCancellationSource = runningCancellationSource;
            }

            var request = new SpineAssetGroupDetailsRefreshRequest(
                groupTab,
                revision,
                signature,
                runningCancellationSource.Token,
                () => IsCurrent(groupTab, entry, revision, signature));
            try
            {
                await _refreshAsync(request);
            }
            catch (OperationCanceledException) when (runningCancellationSource.IsCancellationRequested)
            {
            }
            catch
            {
                // 刷新失败只丢弃本次预览请求；操作事实和读模型仍由下一次通知恢复。
            }
            finally
            {
                runningCancellationSource.Dispose();
                lock (_gate)
                {
                    if (ReferenceEquals(entry.RunningCancellationSource, runningCancellationSource))
                    {
                        entry.RunningCancellationSource = null;
                    }
                }
            }

            lock (_gate)
            {
                if (entry.HasPendingRequest && !entry.IsCancelled && !_isDisposed)
                {
                    continue;
                }

                StopEntry(groupTab, entry);
                return;
            }
        }
    }

    private bool TryPrepareNextDelay(
        SpineAssetGroupDocumentTabViewModel groupTab,
        RefreshEntry entry,
        out long revision,
        out string signature,
        out CancellationTokenSource delayCancellationSource)
    {
        revision = 0;
        signature = string.Empty;
        delayCancellationSource = null!;
        if (entry.IsCancelled || _isDisposed || !entry.HasPendingRequest)
        {
            StopEntry(groupTab, entry);
            return false;
        }

        entry.HasPendingRequest = false;
        revision = entry.Revision;
        signature = entry.LatestSignature;
        delayCancellationSource = new CancellationTokenSource();
        entry.DelayCancellationSource = delayCancellationSource;
        return true;
    }

    private void StopEntry(
        SpineAssetGroupDocumentTabViewModel groupTab,
        RefreshEntry entry)
    {
        if (_entries.TryGetValue(groupTab, out var currentEntry) &&
            ReferenceEquals(currentEntry, entry))
        {
            _entries.Remove(groupTab);
        }

        entry.IsWorkerActive = false;
        Complete(entry.TakeCompletions());
    }

    private bool IsCurrent(
        SpineAssetGroupDocumentTabViewModel groupTab,
        RefreshEntry entry,
        long revision,
        string signature)
    {
        lock (_gate)
        {
            return !_isDisposed &&
                   !entry.IsCancelled &&
                   _entries.TryGetValue(groupTab, out var currentEntry) &&
                   ReferenceEquals(currentEntry, entry) &&
                   entry.Revision == revision &&
                   string.Equals(entry.LatestSignature, signature, StringComparison.Ordinal);
        }
    }

    private void ClearDelayCancellationSource(
        RefreshEntry entry,
        CancellationTokenSource delayCancellationSource)
    {
        lock (_gate)
        {
            if (ReferenceEquals(entry.DelayCancellationSource, delayCancellationSource))
            {
                entry.DelayCancellationSource = null;
            }
        }
    }

    private static void TryCancel(CancellationTokenSource? cancellationSource)
    {
        if (cancellationSource is null)
        {
            return;
        }

        try
        {
            cancellationSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static void Complete(IEnumerable<TaskCompletionSource> completions)
    {
        foreach (var completion in completions)
        {
            completion.TrySetResult();
        }
    }

    private sealed class RefreshEntry
    {
        public long Revision { get; set; }

        public string LatestSignature { get; set; } = string.Empty;

        public bool HasPendingRequest { get; set; }

        public bool IsWorkerActive { get; set; }

        public bool IsCancelled { get; set; }

        public CancellationTokenSource? DelayCancellationSource { get; set; }

        public CancellationTokenSource? RunningCancellationSource { get; set; }

        public List<TaskCompletionSource> PendingCompletions { get; } = [];

        public TaskCompletionSource[] TakeCompletions()
        {
            var completions = PendingCompletions.ToArray();
            PendingCompletions.Clear();
            return completions;
        }
    }
}

internal sealed class SpineAssetGroupDetailsRefreshRequest
{
    private readonly Func<bool> _isCurrent;

    public SpineAssetGroupDetailsRefreshRequest(
        SpineAssetGroupDocumentTabViewModel groupTab,
        long revision,
        string inputSignature,
        CancellationToken cancellationToken,
        Func<bool> isCurrent)
    {
        GroupTab = groupTab ?? throw new ArgumentNullException(nameof(groupTab));
        Revision = revision;
        InputSignature = inputSignature ?? throw new ArgumentNullException(nameof(inputSignature));
        CancellationToken = cancellationToken;
        _isCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
    }

    public SpineAssetGroupDocumentTabViewModel GroupTab { get; }

    public long Revision { get; }

    public string InputSignature { get; }

    public CancellationToken CancellationToken { get; }

    public bool IsCurrent => !CancellationToken.IsCancellationRequested && _isCurrent();
}
