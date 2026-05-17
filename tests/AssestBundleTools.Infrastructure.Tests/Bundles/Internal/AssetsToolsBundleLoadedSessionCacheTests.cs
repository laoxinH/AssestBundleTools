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
}
