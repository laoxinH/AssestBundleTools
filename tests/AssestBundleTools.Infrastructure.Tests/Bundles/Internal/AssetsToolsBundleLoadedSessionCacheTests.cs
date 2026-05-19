using AssetsTools.NET.Extra;
using AssestBundleTools.Infrastructure.Bundles.Internal;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Internal;

internal static class AssetsToolsBundleLoadedSessionCacheTests
{
    public static async Task AcquireAsync_ShouldShareInFlightLoad_ForSameBundlePath()
    {
        using var loaderEntered = new ManualResetEventSlim();
        using var releaseLoader = new ManualResetEventSlim();
        var loadCount = 0;
        using var cache = new AssetsToolsBundleLoadedSessionCache(
            _ =>
            {
                Interlocked.Increment(ref loadCount);
                loaderEntered.Set();
                releaseLoader.Wait(TimeSpan.FromSeconds(5));
                throw new InvalidOperationException("load failed");
            },
            TimeSpan.FromSeconds(30));
        var bundlePath = Path.Combine(Path.GetTempPath(), $"bundle-{Guid.NewGuid():N}.ab");

        var firstTask = cache.AcquireAsync(bundlePath, CancellationToken.None);
        TestAssert.True(
            loaderEntered.Wait(TimeSpan.FromSeconds(5)),
            "The first acquire should start the physical load.");

        var secondTask = cache.AcquireAsync(bundlePath, CancellationToken.None);
        await Task.Delay(50);

        TestAssert.Equal(
            1,
            Volatile.Read(ref loadCount),
            "Concurrent acquires for the same bundle path should share one in-flight load.");

        releaseLoader.Set();
        await AssertLoadFailedAsync(firstTask);
        await AssertLoadFailedAsync(secondTask);
    }

    public static async Task AcquireAsync_ShouldShareInFlightLoad_ForSameExplicitCacheKey()
    {
        using var loaderEntered = new ManualResetEventSlim();
        using var releaseLoader = new ManualResetEventSlim();
        var loadCount = 0;
        using var cache = new AssetsToolsBundleLoadedSessionCache(
            _ =>
            {
                Interlocked.Increment(ref loadCount);
                loaderEntered.Set();
                releaseLoader.Wait(TimeSpan.FromSeconds(5));
                throw new InvalidOperationException("load failed");
            },
            TimeSpan.FromSeconds(30));
        var firstBundlePath = Path.Combine(Path.GetTempPath(), $"bundle-{Guid.NewGuid():N}.ab");
        var secondBundlePath = Path.Combine(Path.GetTempPath(), $"bundle-{Guid.NewGuid():N}.ab");
        const string sessionCacheKey = "artifact|workspace|bundle|fingerprint-a";

        var firstTask = cache.AcquireAsync(firstBundlePath, sessionCacheKey, CancellationToken.None);
        TestAssert.True(
            loaderEntered.Wait(TimeSpan.FromSeconds(5)),
            "The first acquire should start the physical load for the explicit identity.");

        var secondTask = cache.AcquireAsync(secondBundlePath, sessionCacheKey, CancellationToken.None);
        await Task.Delay(50);

        TestAssert.Equal(
            1,
            Volatile.Read(ref loadCount),
            "Concurrent acquires with the same explicit artifact identity should share one in-flight load.");

        releaseLoader.Set();
        await AssertLoadFailedAsync(firstTask);
        await AssertLoadFailedAsync(secondTask);
    }

    public static async Task AcquireAsync_ShouldNotShareInFlightLoad_WhenExplicitCacheKeysDiffer()
    {
        using var bothLoadersEntered = new ManualResetEventSlim();
        using var releaseLoaders = new ManualResetEventSlim();
        var loadCount = 0;
        using var cache = new AssetsToolsBundleLoadedSessionCache(
            _ =>
            {
                if (Interlocked.Increment(ref loadCount) == 2)
                {
                    bothLoadersEntered.Set();
                }

                releaseLoaders.Wait(TimeSpan.FromSeconds(5));
                throw new InvalidOperationException("load failed");
            },
            TimeSpan.FromSeconds(30));
        var bundlePath = Path.Combine(Path.GetTempPath(), $"bundle-{Guid.NewGuid():N}.ab");

        var firstTask = cache.AcquireAsync(bundlePath, "artifact|workspace|bundle|fingerprint-a", CancellationToken.None);
        var secondTask = cache.AcquireAsync(bundlePath, "artifact|workspace|bundle|fingerprint-b", CancellationToken.None);

        TestAssert.True(
            bothLoadersEntered.Wait(TimeSpan.FromSeconds(5)),
            "Different explicit artifact identities should start separate physical load tasks.");
        TestAssert.Equal(
            2,
            Volatile.Read(ref loadCount),
            "Different artifact fingerprints for the same physical path should not reuse the old loaded session.");

        releaseLoaders.Set();
        await AssertLoadFailedAsync(firstTask);
        await AssertLoadFailedAsync(secondTask);
    }

    public static async Task AcquireAsync_ShouldRemoveFailedLoadEntry()
    {
        var loadCount = 0;
        using var cache = new AssetsToolsBundleLoadedSessionCache(
            _ =>
            {
                Interlocked.Increment(ref loadCount);
                throw new InvalidOperationException("load failed");
            },
            TimeSpan.FromSeconds(30));
        var bundlePath = Path.Combine(Path.GetTempPath(), $"bundle-{Guid.NewGuid():N}.ab");

        await AssertLoadFailedAsync(cache.AcquireAsync(bundlePath, CancellationToken.None));
        await AssertLoadFailedAsync(cache.AcquireAsync(bundlePath, CancellationToken.None));

        TestAssert.Equal(
            2,
            Volatile.Read(ref loadCount),
            "A failed physical load should be evicted so a later request can retry.");
    }

    public static async Task AcquireAsync_ShouldRetainInFlightLoad_WhenWaitingAcquireIsCancelled()
    {
        using var loaderEntered = new ManualResetEventSlim();
        using var releaseLoader = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var loadCount = 0;
        using var cache = new AssetsToolsBundleLoadedSessionCache(
            path =>
            {
                Interlocked.Increment(ref loadCount);
                loaderEntered.Set();
                releaseLoader.Wait(TimeSpan.FromSeconds(5));
                return CreateLoadedSession(path);
            },
            TimeSpan.FromSeconds(30));
        var bundlePath = Path.Combine(Path.GetTempPath(), $"bundle-{Guid.NewGuid():N}.ab");
        const string sessionCacheKey = "bundle-artifact|workspace-a|bundle-a|normalizer|fingerprint-a";

        var cancelledAcquireTask = cache.AcquireAsync(bundlePath, sessionCacheKey, cancellation.Token);
        TestAssert.True(
            loaderEntered.Wait(TimeSpan.FromSeconds(5)),
            "The first acquire should start the physical load.");

        cancellation.Cancel();
        await AssertCanceledAsync(cancelledAcquireTask);
        releaseLoader.Set();

        using var retainedLease = await cache.AcquireAsync(bundlePath, sessionCacheKey, CancellationToken.None);
        await retainedLease.Session.ExecutionGate.WaitAsync(CancellationToken.None);
        retainedLease.Session.ExecutionGate.Release();

        TestAssert.Equal(
            1,
            Volatile.Read(ref loadCount),
            "Cancelling a waiter should not dispose the retained in-flight session or force a second physical load.");
    }

    public static async Task AcquireAsync_ShouldEvictLeastRecentlyUsedIdleSession_WhenCapacityIsExceeded()
    {
        var loadCount = 0;
        using var cache = new AssetsToolsBundleLoadedSessionCache(
            path =>
            {
                Interlocked.Increment(ref loadCount);
                return CreateLoadedSession(path);
            },
            TimeSpan.FromMinutes(5),
            maximumRetainedSessions: 1,
            memoryPressureRetainedSessions: 1,
            memoryPressureEvaluator: static () => false);
        var firstBundlePath = Path.Combine(Path.GetTempPath(), $"bundle-a-{Guid.NewGuid():N}.ab");
        var secondBundlePath = Path.Combine(Path.GetTempPath(), $"bundle-b-{Guid.NewGuid():N}.ab");

        using (await cache.AcquireAsync(firstBundlePath, "bundle-artifact|workspace-a|bundle-a|normalizer|fingerprint-a", CancellationToken.None))
        {
        }

        using (await cache.AcquireAsync(secondBundlePath, "bundle-artifact|workspace-a|bundle-b|normalizer|fingerprint-b", CancellationToken.None))
        {
        }

        using (await cache.AcquireAsync(firstBundlePath, "bundle-artifact|workspace-a|bundle-a|normalizer|fingerprint-a", CancellationToken.None))
        {
        }

        TestAssert.Equal(
            3,
            Volatile.Read(ref loadCount),
            "The idle loaded session over the capacity limit should be evicted and loaded again on the next acquire.");
    }

    public static async Task AcquireAsync_ShouldRetreatToPressureLimit_WhenMemoryPressureIsReported()
    {
        var loadCount = 0;
        using var cache = new AssetsToolsBundleLoadedSessionCache(
            path =>
            {
                Interlocked.Increment(ref loadCount);
                return CreateLoadedSession(path);
            },
            TimeSpan.FromMinutes(5),
            maximumRetainedSessions: 4,
            memoryPressureRetainedSessions: 1,
            memoryPressureEvaluator: static () => true);
        var firstBundlePath = Path.Combine(Path.GetTempPath(), $"bundle-a-{Guid.NewGuid():N}.ab");
        var secondBundlePath = Path.Combine(Path.GetTempPath(), $"bundle-b-{Guid.NewGuid():N}.ab");

        using (await cache.AcquireAsync(firstBundlePath, "bundle-artifact|workspace-a|bundle-a|normalizer|fingerprint-a", CancellationToken.None))
        {
        }

        using (await cache.AcquireAsync(secondBundlePath, "bundle-artifact|workspace-a|bundle-b|normalizer|fingerprint-b", CancellationToken.None))
        {
        }

        using (await cache.AcquireAsync(firstBundlePath, "bundle-artifact|workspace-a|bundle-a|normalizer|fingerprint-a", CancellationToken.None))
        {
        }

        TestAssert.Equal(
            3,
            Volatile.Read(ref loadCount),
            "Memory pressure should trim idle loaded sessions down to the pressure retention limit.");
    }

    public static async Task InvalidateBundleArtifact_ShouldRemoveMatchingArtifactIdentity()
    {
        using var firstLoaderEntered = new ManualResetEventSlim();
        using var secondLoaderEntered = new ManualResetEventSlim();
        using var releaseLoaders = new ManualResetEventSlim();
        var loadCount = 0;
        using var cache = new AssetsToolsBundleLoadedSessionCache(
            _ =>
            {
                var currentCount = Interlocked.Increment(ref loadCount);
                if (currentCount == 1)
                {
                    firstLoaderEntered.Set();
                }

                if (currentCount == 2)
                {
                    secondLoaderEntered.Set();
                }

                releaseLoaders.Wait(TimeSpan.FromSeconds(5));
                throw new InvalidOperationException("load failed");
            },
            TimeSpan.FromSeconds(30));
        var bundlePath = Path.Combine(Path.GetTempPath(), $"bundle-{Guid.NewGuid():N}.ab");
        const string sessionCacheKey = "bundle-artifact|workspace-a|bundle-a|normalizer|fingerprint-a";

        var firstTask = cache.AcquireAsync(bundlePath, sessionCacheKey, CancellationToken.None);
        TestAssert.True(
            firstLoaderEntered.Wait(TimeSpan.FromSeconds(5)),
            "The first acquire should start the first physical load.");

        cache.InvalidateBundleArtifact("workspace-a", "bundle-a");
        var secondTask = cache.AcquireAsync(bundlePath, sessionCacheKey, CancellationToken.None);

        TestAssert.True(
            secondLoaderEntered.Wait(TimeSpan.FromSeconds(5)),
            "Invalidating a bundle artifact should let the next acquire start a fresh load.");
        TestAssert.Equal(
            2,
            Volatile.Read(ref loadCount),
            "The invalidated artifact identity should no longer share the old in-flight entry.");

        releaseLoaders.Set();
        await AssertLoadFailedAsync(firstTask);
        await AssertLoadFailedAsync(secondTask);
    }

    public static async Task InvalidateWorkspace_ShouldRemoveMatchingWorkspaceArtifactIdentities()
    {
        using var firstLoaderEntered = new ManualResetEventSlim();
        using var secondLoaderEntered = new ManualResetEventSlim();
        using var releaseLoaders = new ManualResetEventSlim();
        var loadCount = 0;
        using var cache = new AssetsToolsBundleLoadedSessionCache(
            _ =>
            {
                var currentCount = Interlocked.Increment(ref loadCount);
                if (currentCount == 1)
                {
                    firstLoaderEntered.Set();
                }

                if (currentCount == 2)
                {
                    secondLoaderEntered.Set();
                }

                releaseLoaders.Wait(TimeSpan.FromSeconds(5));
                throw new InvalidOperationException("load failed");
            },
            TimeSpan.FromSeconds(30));
        var bundlePath = Path.Combine(Path.GetTempPath(), $"bundle-{Guid.NewGuid():N}.ab");
        const string sessionCacheKey = "bundle-artifact|workspace-a|bundle-a|normalizer|fingerprint-a";

        var firstTask = cache.AcquireAsync(bundlePath, sessionCacheKey, CancellationToken.None);
        TestAssert.True(
            firstLoaderEntered.Wait(TimeSpan.FromSeconds(5)),
            "The first acquire should start the first physical load.");

        cache.InvalidateWorkspace("workspace-a");
        var secondTask = cache.AcquireAsync(bundlePath, sessionCacheKey, CancellationToken.None);

        TestAssert.True(
            secondLoaderEntered.Wait(TimeSpan.FromSeconds(5)),
            "Invalidating a workspace should let matching artifact identities start fresh loads.");
        TestAssert.Equal(
            2,
            Volatile.Read(ref loadCount),
            "Workspace invalidation should remove existing artifact identity entries for that workspace.");

        releaseLoaders.Set();
        await AssertLoadFailedAsync(firstTask);
        await AssertLoadFailedAsync(secondTask);
    }

    private static async Task AssertLoadFailedAsync(Task<AssetsToolsBundleLoadedSessionLease> task)
    {
        try
        {
            await task;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException("Expected the bundle load task to fail.");
    }

    private static async Task AssertCanceledAsync(Task<AssetsToolsBundleLoadedSessionLease> task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        throw new InvalidOperationException("Expected the bundle load task to be cancelled.");
    }

    private static AssetsToolsBundleLoadedSession CreateLoadedSession(string bundlePath)
    {
        return new AssetsToolsBundleLoadedSession(
            bundlePath,
            new AssetsManager(),
            bundleInstance: null!,
            Array.Empty<AssetsFileInstance>(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }
}
