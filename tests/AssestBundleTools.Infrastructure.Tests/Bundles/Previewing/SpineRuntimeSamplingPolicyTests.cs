using AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Previewing;

internal static class SpineRuntimeSamplingPolicyTests
{
    public static void CreateSampleTimes_ShouldKeepThirtyFpsBudget_ForLargeOfficialRuntimeMesh()
    {
        var budget = SpineRuntimeSamplingPolicy.CreateBudget(
            drawCommandCount: 25,
            meshTriangleCount: 5000);

        var sampleTimes = SpineRuntimeSamplingPolicy.CreateSampleTimes(4f, budget);

        TestAssert.Equal(120, sampleTimes.Count, "Large official runtime meshes should stay close to 30 FPS within the memory-bounded preview cap.");
    }

    public static void CreateSampleTimes_ShouldStayBounded_ForExtremeOfficialRuntimeMesh()
    {
        var budget = SpineRuntimeSamplingPolicy.CreateBudget(
            drawCommandCount: 1200,
            meshTriangleCount: 30000);

        var sampleTimes = SpineRuntimeSamplingPolicy.CreateSampleTimes(8f, budget);

        TestAssert.True(sampleTimes.Count <= 120, "Runtime sampling should stay within the memory-bounded hard frame cap.");
        TestAssert.True(sampleTimes.Count >= 2, "Runtime sampling should keep at least two frames for animated previews.");
    }
}
