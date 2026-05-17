namespace AssestBundleTools.Infrastructure.Bundles.Previewing;

/// <summary>
/// 为预览链路提供 bundle 级 single-flight 和短时 loaded-session 复用。
/// </summary>
internal sealed class AssetsToolsBundlePreviewSessionCache
{
    private static readonly TimeSpan DefaultIdleRetention = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CleanupPeriod = TimeSpan.FromSeconds(10);

    private readonly object _syncRoot = new();
    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, AssetsToolsBundlePreviewSession> _sessionLoader;
    private readonly TimeSpan _idleRetention;
    private readonly Timer _cleanupTimer;

    public AssetsToolsBundlePreviewSessionCache()
        : this(AssetsToolsBundlePreviewSession.Load, DefaultIdleRetention)
    {
    }

    internal AssetsToolsBundlePreviewSessionCache(
        Func<string, AssetsToolsBundlePreviewSession> sessionLoader,
        TimeSpan idleRetention)
    {
        _sessionLoader = sessionLoader ?? throw new ArgumentNullException(nameof(sessionLoader));
        _idleRetention = idleRetention > TimeSpan.Zero
            ? idleRetention
            : DefaultIdleRetention;
        _cleanupTimer = new Timer(
            static state => ((AssetsToolsBundlePreviewSessionCache)state!).TrimExpiredEntries(),
            this,
            CleanupPeriod,
            CleanupPeriod);
    }

    public async Task<AssetsToolsBundlePreviewSessionLease> AcquireAsync(
        string bundlePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bundlePath))
        {
            throw new ArgumentException("Bundle path is required.", nameof(bundlePath));
        }

        var cacheKey = Path.GetFullPath(bundlePath);
        var entry = GetOrCreateEntry(cacheKey);
        try
        {
            var session = await entry.SessionTask.WaitAsync(cancellationToken);
            return new AssetsToolsBundlePreviewSessionLease(session, () => Release(cacheKey, entry));
        }
        catch
        {
            Release(cacheKey, entry, removeEntry: entry.SessionTask.IsCompleted);
            throw;
        }
    }

    private CacheEntry GetOrCreateEntry(string cacheKey)
    {
        lock (_syncRoot)
        {
            if (!_entries.TryGetValue(cacheKey, out var entry))
            {
                entry = new CacheEntry(Task.Run(() => _sessionLoader(cacheKey)));
                _entries[cacheKey] = entry;
            }

            entry.LeaseCount++;
            entry.LastAccessUtc = DateTimeOffset.UtcNow;
            return entry;
        }
    }

    private void Release(string cacheKey, CacheEntry entry, bool removeEntry = false)
    {
        AssetsToolsBundlePreviewSession? disposableSession = null;
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
        List<AssetsToolsBundlePreviewSession> disposableSessions = [];
        var now = DateTimeOffset.UtcNow;
        lock (_syncRoot)
        {
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
        public CacheEntry(Task<AssetsToolsBundlePreviewSession> sessionTask)
        {
            SessionTask = sessionTask;
            LastAccessUtc = DateTimeOffset.UtcNow;
        }

        public Task<AssetsToolsBundlePreviewSession> SessionTask { get; }

        public int LeaseCount { get; set; }

        public DateTimeOffset LastAccessUtc { get; set; }
    }
}

internal sealed class AssetsToolsBundlePreviewSessionLease : IDisposable
{
    private readonly Action _release;
    private int _isDisposed;

    public AssetsToolsBundlePreviewSessionLease(
        AssetsToolsBundlePreviewSession session,
        Action release)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _release = release ?? throw new ArgumentNullException(nameof(release));
    }

    public AssetsToolsBundlePreviewSession Session { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            _release();
        }
    }
}
