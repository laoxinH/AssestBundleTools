namespace AssestBundleTools.Infrastructure.Bundles.Internal;

/// <summary>
/// 为 AssetsTools bundle 访问提供 bundle 级 single-flight 和短时 loaded-session 复用。
/// </summary>
internal sealed class AssetsToolsBundleLoadedSessionCache : IDisposable
{
    private static readonly TimeSpan DefaultIdleRetention = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CleanupPeriod = TimeSpan.FromSeconds(10);

    private readonly object _syncRoot = new();
    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, AssetsToolsBundleLoadedSession> _sessionLoader;
    private readonly TimeSpan _idleRetention;
    private readonly Timer _cleanupTimer;
    private bool _isDisposed;

    public static AssetsToolsBundleLoadedSessionCache Shared { get; } = new();

    public AssetsToolsBundleLoadedSessionCache()
        : this(AssetsToolsBundleLoadedSession.Load, DefaultIdleRetention)
    {
    }

    internal AssetsToolsBundleLoadedSessionCache(
        Func<string, AssetsToolsBundleLoadedSession> sessionLoader,
        TimeSpan idleRetention)
    {
        _sessionLoader = sessionLoader ?? throw new ArgumentNullException(nameof(sessionLoader));
        _idleRetention = idleRetention > TimeSpan.Zero
            ? idleRetention
            : DefaultIdleRetention;
        _cleanupTimer = new Timer(
            static state => ((AssetsToolsBundleLoadedSessionCache)state!).TrimExpiredEntries(),
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
                entry = new CacheEntry(Task.Run(() => _sessionLoader(bundlePath)));
                _entries[cacheKey] = entry;
            }

            entry.LeaseCount++;
            entry.LastAccessUtc = DateTimeOffset.UtcNow;
            return entry;
        }
    }

    private void Release(string cacheKey, CacheEntry entry, bool removeEntry = false)
    {
        AssetsToolsBundleLoadedSession? disposableSession = null;
        lock (_syncRoot)
        {
            if (entry.LeaseCount > 0)
            {
                entry.LeaseCount--;
            }

            entry.LastAccessUtc = DateTimeOffset.UtcNow;
            if (!removeEntry || entry.LeaseCount > 0)
            {
                return;
            }

            if (_entries.TryGetValue(cacheKey, out var currentEntry) && ReferenceEquals(currentEntry, entry))
            {
                _entries.Remove(cacheKey);
                if (entry.SessionTask.IsCompletedSuccessfully)
                {
                    disposableSession = entry.SessionTask.Result;
                }
            }
        }

        disposableSession?.Dispose();
    }

    private void TrimExpiredEntries()
    {
        List<AssetsToolsBundleLoadedSession> disposableSessions = [];
        var now = DateTimeOffset.UtcNow;
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
                }
            }
        }

        foreach (var session in disposableSessions)
        {
            session.Dispose();
        }
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
