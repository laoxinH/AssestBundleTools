using System.Diagnostics;

namespace AssestBundleTools.Infrastructure.Bundles.Internal;

/// <summary>
/// 为 AssetsTools bundle 访问提供 bundle 级 single-flight 和短时 loaded-session 复用。
/// </summary>
internal sealed class AssetsToolsBundleLoadedSessionCache : IDisposable
{
    private static readonly TimeSpan DefaultIdleRetention = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CleanupPeriod = TimeSpan.FromSeconds(10);
    private const int DefaultMaximumRetainedSessions = 6;
    private const int DefaultMemoryPressureRetainedSessions = 1;
    private const double DefaultMemoryPressureThresholdRatio = 0.85;

    private readonly object _syncRoot = new();
    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, AssetsToolsBundleLoadedSession> _sessionLoader;
    private readonly TimeSpan _idleRetention;
    private readonly int _maximumRetainedSessions;
    private readonly int _memoryPressureRetainedSessions;
    private readonly Func<bool> _memoryPressureEvaluator;
    private readonly Timer _cleanupTimer;
    private bool _isDisposed;

    public static AssetsToolsBundleLoadedSessionCache Shared { get; } = new();

    public AssetsToolsBundleLoadedSessionCache()
        : this(AssetsToolsBundleLoadService.LoadIsolated, DefaultIdleRetention)
    {
    }

    internal AssetsToolsBundleLoadedSessionCache(
        Func<string, AssetsToolsBundleLoadedSession> sessionLoader,
        TimeSpan idleRetention)
        : this(
            sessionLoader,
            idleRetention,
            DefaultMaximumRetainedSessions,
            DefaultMemoryPressureRetainedSessions,
            IsProcessUnderMemoryPressure)
    {
    }

    internal AssetsToolsBundleLoadedSessionCache(
        Func<string, AssetsToolsBundleLoadedSession> sessionLoader,
        TimeSpan idleRetention,
        int maximumRetainedSessions,
        int memoryPressureRetainedSessions,
        Func<bool>? memoryPressureEvaluator = null)
    {
        _sessionLoader = sessionLoader ?? throw new ArgumentNullException(nameof(sessionLoader));
        _idleRetention = idleRetention > TimeSpan.Zero
            ? idleRetention
            : DefaultIdleRetention;
        _maximumRetainedSessions = Math.Max(1, maximumRetainedSessions);
        _memoryPressureRetainedSessions = Math.Clamp(
            memoryPressureRetainedSessions,
            0,
            _maximumRetainedSessions);
        _memoryPressureEvaluator = memoryPressureEvaluator ?? IsProcessUnderMemoryPressure;
        _cleanupTimer = new Timer(
            static state => ((AssetsToolsBundleLoadedSessionCache)state!).TrimRetainedEntries(),
            this,
            CleanupPeriod,
            CleanupPeriod);
    }

    public async Task<AssetsToolsBundleLoadedSessionLease> AcquireAsync(
        string bundlePath,
        CancellationToken cancellationToken)
    {
        return await AcquireAsync(bundlePath, sessionCacheKey: null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssetsToolsBundleLoadedSessionLease> AcquireAsync(
        string bundlePath,
        string? sessionCacheKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bundlePath))
        {
            throw new ArgumentException("Bundle path is required.", nameof(bundlePath));
        }

        var resolvedBundlePath = Path.GetFullPath(bundlePath);
        var cacheKey = ResolveCacheKey(resolvedBundlePath, sessionCacheKey);
        var entry = GetOrCreateEntry(cacheKey, resolvedBundlePath);
        TrimRetainedEntries();
        try
        {
            var session = await entry.SessionTask.WaitAsync(cancellationToken);
            return new AssetsToolsBundleLoadedSessionLease(session, () => Release(cacheKey, entry));
        }
        catch
        {
            Release(cacheKey, entry, removeEntry: entry.SessionTask.IsCompleted);
            throw;
        }
    }

    public void InvalidateBundleArtifact(string workspaceKey, string bundleCacheKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) ||
            string.IsNullOrWhiteSpace(bundleCacheKey))
        {
            return;
        }

        InvalidateByPrefix(string.Join(
            '|',
            "bundle-artifact",
            workspaceKey.Trim(),
            bundleCacheKey.Trim(),
            string.Empty));
    }

    public void InvalidateWorkspace(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return;
        }

        InvalidateByPrefix(string.Join(
            '|',
            "bundle-artifact",
            workspaceKey.Trim(),
            string.Empty));
    }

    public void Dispose()
    {
        List<AssetsToolsBundleLoadedSession> disposableSessions = [];
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            foreach (var entry in _entries.Values)
            {
                if (entry.SessionTask.IsCompletedSuccessfully)
                {
                    disposableSessions.Add(entry.SessionTask.Result);
                }
            }

            _entries.Clear();
        }

        _cleanupTimer.Dispose();
        foreach (var session in disposableSessions)
        {
            session.Dispose();
        }
    }

    private static string ResolveCacheKey(string resolvedBundlePath, string? sessionCacheKey)
    {
        return string.IsNullOrWhiteSpace(sessionCacheKey)
            ? resolvedBundlePath
            : sessionCacheKey.Trim();
    }

    private CacheEntry GetOrCreateEntry(string cacheKey, string bundlePath)
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            if (!_entries.TryGetValue(cacheKey, out var entry))
            {
                entry = new CacheEntry(Task.Run(() => LoadSessionWithDiagnostics(bundlePath, cacheKey)));
                _entries[cacheKey] = entry;
                WriteDiagnostic(
                    $"Acquire miss. Key={cacheKey}; LeasesAfter=1; Entries={_entries.Count}; Path={bundlePath}.");
            }
            else
            {
                WriteDiagnostic(
                    $"Acquire {ResolveAcquireDiagnosticKind(entry)}. Key={cacheKey}; LeasesAfter={entry.LeaseCount + 1}; Entries={_entries.Count}; Path={bundlePath}.");
            }

            entry.LeaseCount++;
            entry.LastAccessUtc = DateTimeOffset.UtcNow;
            return entry;
        }
    }

    private AssetsToolsBundleLoadedSession LoadSessionWithDiagnostics(string bundlePath, string cacheKey)
    {
        var stopwatch = Stopwatch.StartNew();
        WriteDiagnostic($"Load start. Key={cacheKey}; Path={bundlePath}.");
        try
        {
            var session = _sessionLoader(bundlePath);
            WriteDiagnostic(
                $"Load completed. Key={cacheKey}; DurationMs={stopwatch.ElapsedMilliseconds}; SerializedFiles={session.SerializedFiles.Count}; Path={bundlePath}.");
            return session;
        }
        catch (Exception ex)
        {
            WriteDiagnostic(
                $"Load failed. Key={cacheKey}; DurationMs={stopwatch.ElapsedMilliseconds}; Error={ex.GetType().Name}; Message={ex.Message}; Path={bundlePath}.");
            throw;
        }
    }

    private static string ResolveAcquireDiagnosticKind(CacheEntry entry) =>
        !entry.SessionTask.IsCompleted ? "in-flight" :
        entry.SessionTask.IsCompletedSuccessfully ? "hit" :
        entry.SessionTask.IsCanceled ? "canceled" :
        entry.SessionTask.IsFaulted ? "faulted" :
        "completed";

    private void Release(string cacheKey, CacheEntry entry, bool removeEntry = false)
    {
        AssetsToolsBundleLoadedSession? disposableSession = null;
        var shouldTrim = false;
        lock (_syncRoot)
        {
            if (entry.LeaseCount > 0)
            {
                entry.LeaseCount--;
            }

            entry.LastAccessUtc = DateTimeOffset.UtcNow;
            if (entry.LeaseCount > 0)
            {
                return;
            }

            if (!removeEntry && !entry.IsInvalidated)
            {
                shouldTrim = true;
            }
            else
            {
                if (_entries.TryGetValue(cacheKey, out var currentEntry) && ReferenceEquals(currentEntry, entry))
                {
                    _entries.Remove(cacheKey);
                }

                disposableSession = TryGetDisposableCompletedSession(entry);
            }
        }

        disposableSession?.Dispose();
        if (!shouldTrim)
        {
            DisposeWhenCompletedIfNeeded(entry);
        }

        if (shouldTrim)
        {
            TrimRetainedEntries();
        }
    }

    private void TrimRetainedEntries()
    {
        List<AssetsToolsBundleLoadedSession> disposableSessions = [];
        var now = DateTimeOffset.UtcNow;
        var isUnderMemoryPressure = IsUnderMemoryPressure();
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            foreach (var pair in _entries.ToArray())
            {
                var entry = pair.Value;
                if (entry.LeaseCount > 0 ||
                    !entry.SessionTask.IsCompleted ||
                    now - entry.LastAccessUtc < _idleRetention)
                {
                    continue;
                }

                _entries.Remove(pair.Key);
                if (entry.SessionTask.IsCompletedSuccessfully)
                {
                    disposableSessions.Add(entry.SessionTask.Result);
                    WriteDiagnostic($"Expired idle loaded session. Key={pair.Key}.");
                }
            }

            var targetRetainedSessions = isUnderMemoryPressure
                ? _memoryPressureRetainedSessions
                : _maximumRetainedSessions;
            TrimOverCapacityEntries(targetRetainedSessions, isUnderMemoryPressure, disposableSessions);
        }

        foreach (var session in disposableSessions)
        {
            session.Dispose();
        }
    }

    private void TrimOverCapacityEntries(
        int targetRetainedSessions,
        bool isUnderMemoryPressure,
        List<AssetsToolsBundleLoadedSession> disposableSessions)
    {
        if (_entries.Count <= targetRetainedSessions)
        {
            return;
        }

        var candidates = _entries
            .Where(static pair => pair.Value.LeaseCount == 0 && pair.Value.SessionTask.IsCompleted)
            .OrderBy(static pair => pair.Value.LastAccessUtc)
            .ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var pair in candidates)
        {
            if (_entries.Count <= targetRetainedSessions)
            {
                break;
            }

            if (!_entries.Remove(pair.Key))
            {
                continue;
            }

            if (pair.Value.SessionTask.IsCompletedSuccessfully)
            {
                disposableSessions.Add(pair.Value.SessionTask.Result);
            }

            var reason = isUnderMemoryPressure ? "memory pressure" : "capacity";
            WriteDiagnostic($"Evicted loaded session due to {reason}. Key={pair.Key}; Target={targetRetainedSessions}.");
        }
    }

    private void InvalidateByPrefix(string keyPrefix)
    {
        List<CacheEntry> invalidatedEntries = [];
        List<AssetsToolsBundleLoadedSession> disposableSessions = [];
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            foreach (var pair in _entries.ToArray())
            {
                if (!pair.Key.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _entries.Remove(pair.Key);
                pair.Value.IsInvalidated = true;
                if (pair.Value.LeaseCount == 0)
                {
                    var disposableSession = TryGetDisposableCompletedSession(pair.Value);
                    if (disposableSession is not null)
                    {
                        disposableSessions.Add(disposableSession);
                    }
                    else
                    {
                        invalidatedEntries.Add(pair.Value);
                    }
                }
            }
        }

        foreach (var session in disposableSessions)
        {
            session.Dispose();
        }

        foreach (var entry in invalidatedEntries)
        {
            DisposeWhenCompletedIfNeeded(entry);
        }
    }

    private static AssetsToolsBundleLoadedSession? TryGetDisposableCompletedSession(CacheEntry entry)
    {
        return entry.LeaseCount == 0 && entry.SessionTask.IsCompletedSuccessfully
            ? entry.SessionTask.Result
            : null;
    }

    private static void DisposeWhenCompletedIfNeeded(CacheEntry entry)
    {
        if (entry.LeaseCount != 0 || entry.SessionTask.IsCompleted)
        {
            return;
        }

        _ = entry.SessionTask.ContinueWith(
            static task =>
            {
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    task.Result.Dispose();
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private bool IsUnderMemoryPressure()
    {
        try
        {
            return _memoryPressureEvaluator();
        }
        catch
        {
            return false;
        }
    }

    private static bool IsProcessUnderMemoryPressure()
    {
        var memoryInfo = GC.GetGCMemoryInfo();
        if (memoryInfo.HighMemoryLoadThresholdBytes <= 0)
        {
            return false;
        }

        return memoryInfo.MemoryLoadBytes >=
               memoryInfo.HighMemoryLoadThresholdBytes * DefaultMemoryPressureThresholdRatio;
    }

    [Conditional("DEBUG")]
    private static void WriteDiagnostic(string message)
    {
        Debug.WriteLine($"[AssetsToolsBundleLoadedSessionCache] {message}");
    }

    private sealed class CacheEntry
    {
        public CacheEntry(Task<AssetsToolsBundleLoadedSession> sessionTask)
        {
            SessionTask = sessionTask;
            LastAccessUtc = DateTimeOffset.UtcNow;
        }

        public Task<AssetsToolsBundleLoadedSession> SessionTask { get; }

        public int LeaseCount { get; set; }

        public DateTimeOffset LastAccessUtc { get; set; }

        public bool IsInvalidated { get; set; }
    }
}

internal sealed class AssetsToolsBundleLoadedSessionLease : IDisposable
{
    private readonly Action _release;
    private int _isDisposed;

    public AssetsToolsBundleLoadedSessionLease(
        AssetsToolsBundleLoadedSession session,
        Action release)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _release = release ?? throw new ArgumentNullException(nameof(release));
    }

    public AssetsToolsBundleLoadedSession Session { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            _release();
        }
    }
}
