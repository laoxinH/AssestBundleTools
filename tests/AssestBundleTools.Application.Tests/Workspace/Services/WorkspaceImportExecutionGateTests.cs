using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Tests.Workspace.Services;

internal static class WorkspaceImportExecutionGateTests
{
    public static async Task ExecuteExclusiveAsync_ShouldSerializeConcurrentOperations_ForSameWorkspaceKey()
    {
        var gate = new WorkspaceImportExecutionGate();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executionOrder = new List<string>();

        var firstTask = gate.ExecuteExclusiveAsync(
            "workspace-a",
            async _ =>
            {
                executionOrder.Add("first-enter");
                firstEntered.SetResult();
                await releaseFirst.Task;
                executionOrder.Add("first-exit");
                return 1;
            });

        await firstEntered.Task;

        var secondTask = gate.ExecuteExclusiveAsync(
            "workspace-a",
            _ =>
            {
                executionOrder.Add("second-enter");
                secondEntered.SetResult();
                return Task.FromResult(2);
            });

        var earlyCompletion = await Task.WhenAny(secondEntered.Task, Task.Delay(150));
        TestAssert.True(
            !ReferenceEquals(earlyCompletion, secondEntered.Task),
            "Second operation should remain blocked until the first one releases the gate.");

        releaseFirst.SetResult();

        await Task.WhenAll(firstTask, secondTask);

        TestAssert.Equal(3, executionOrder.Count, "Execution order should contain first enter, first exit and second enter.");
        TestAssert.Equal("first-enter", executionOrder[0], "First operation should enter first.");
        TestAssert.Equal("first-exit", executionOrder[1], "First operation should exit before second enters.");
        TestAssert.Equal("second-enter", executionOrder[2], "Second operation should enter after the first exits.");
    }

    public static async Task ExecuteExclusiveAsync_ShouldRemainReusable_AfterConcurrentImportWaves()
    {
        var gate = new WorkspaceImportExecutionGate();
        var probe = new GateConcurrencyProbe();

        await RunWaveAsync(gate, probe);
        await RunWaveAsync(gate, probe);

        TestAssert.Equal(1, probe.MaxConcurrentOperations, "Same-workspace gate should serialize every operation across repeated waves.");
    }

    private static async Task RunWaveAsync(
        WorkspaceImportExecutionGate gate,
        GateConcurrencyProbe probe)
    {
        var tasks = Enumerable.Range(0, 32)
            .Select(index => gate.ExecuteExclusiveAsync(
                "workspace-a",
                async _ =>
                {
                    var current = Interlocked.Increment(ref probe.ActiveOperations);
                    RecordMaximum(probe, current);
                    await Task.Delay(index % 3);
                    Interlocked.Decrement(ref probe.ActiveOperations);
                    return index;
                }))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    private static void RecordMaximum(GateConcurrencyProbe probe, int candidate)
    {
        while (true)
        {
            var current = probe.MaxConcurrentOperations;
            if (candidate <= current)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref probe.MaxConcurrentOperations, candidate, current) == current)
            {
                return;
            }
        }
    }

    private sealed class GateConcurrencyProbe
    {
        public int ActiveOperations;

        public int MaxConcurrentOperations;
    }
}
