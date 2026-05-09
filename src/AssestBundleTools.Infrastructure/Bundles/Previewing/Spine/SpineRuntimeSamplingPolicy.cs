namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;

internal static class SpineRuntimeSamplingPolicy
{
    private const double SampledFrameRate = 60d;
    private const int MaxSampledFrameCount = 1024;
    private const int MaxSampledDrawCommandBudget = 720000;
    private const int MaxSampledMeshTriangleBudget = 10000000;

    public static SpineRuntimeSamplingBudget CreateBudget(int drawCommandCount, int meshTriangleCount)
    {
        var safeDrawCommandCount = Math.Max(drawCommandCount, 1);
        var safeMeshTriangleCount = Math.Max(meshTriangleCount, 0);
        var maxByDrawCommands = MaxSampledDrawCommandBudget / safeDrawCommandCount;
        var maxByMeshTriangles = safeMeshTriangleCount > 0
            ? MaxSampledMeshTriangleBudget / safeMeshTriangleCount
            : MaxSampledFrameCount;
        var maxFrameCount = Math.Clamp(
            Math.Min(maxByDrawCommands, maxByMeshTriangles),
            2,
            MaxSampledFrameCount);
        return new SpineRuntimeSamplingBudget(maxFrameCount);
    }

    public static IReadOnlyList<double> CreateSampleTimes(
        float duration,
        SpineRuntimeSamplingBudget samplingBudget)
    {
        if (duration <= 0f)
        {
            return [0d];
        }

        var frameCount = Math.Clamp(
            (int)Math.Floor(duration * SampledFrameRate) + 1,
            2,
            samplingBudget.MaxFrameCount);
        var step = duration / Math.Max(frameCount - 1, 1);
        return Enumerable.Range(0, frameCount)
            .Select(index => index == frameCount - 1 ? duration : index * step)
            .Select(static value => (double)value)
            .ToArray();
    }
}

internal readonly record struct SpineRuntimeSamplingBudget(int MaxFrameCount);
