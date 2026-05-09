using AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Previewing;

internal static class SpineRuntimeSamplingPolicyTests
{
    public static void CreateSampleTimes_ShouldKeepHalfSpeedPlaybackDense_ForLargeOfficialRuntimeMesh()
    {
        var budget = SpineRuntimeSamplingPolicy.CreateBudget(
            drawCommandCount: 25,
            meshTriangleCount: 5000);

        var sampleTimes = SpineRuntimeSamplingPolicy.CreateSampleTimes(4f, budget);

        TestAssert.Equal(241, sampleTimes.Count, "Large official runtime meshes should stay close to 60 animation FPS so 0.5x playback remains visually dense within the memory-bounded preview cap.");
        TestAssert.Equal(4d, sampleTimes[^1], "Runtime sampling should keep the exact animation end time.");
    }

    public static void CreateSampleTimes_ShouldKeepLongHalfSpeedAnimationDense_ForLargeOfficialRuntimeMesh()
    {
        var budget = SpineRuntimeSamplingPolicy.CreateBudget(
            drawCommandCount: 25,
            meshTriangleCount: 8500);

        var sampleTimes = SpineRuntimeSamplingPolicy.CreateSampleTimes(16f, budget);

        TestAssert.Equal(961, sampleTimes.Count, "Long large official runtime animations should not collapse to a low visible frame rate at 0.5x playback.");
        TestAssert.Equal(16d, sampleTimes[^1], "Runtime sampling should keep the exact long animation end time.");
    }

    public static void CreateSampleTimes_ShouldStayBounded_ForExtremeOfficialRuntimeMesh()
    {
        var budget = SpineRuntimeSamplingPolicy.CreateBudget(
            drawCommandCount: 1200,
            meshTriangleCount: 30000);

        var sampleTimes = SpineRuntimeSamplingPolicy.CreateSampleTimes(8f, budget);

        TestAssert.True(sampleTimes.Count <= 1024, "Runtime sampling should stay within the memory-bounded hard frame cap.");
        TestAssert.True(sampleTimes.Count >= 2, "Runtime sampling should keep at least two frames for animated previews.");
    }
}
