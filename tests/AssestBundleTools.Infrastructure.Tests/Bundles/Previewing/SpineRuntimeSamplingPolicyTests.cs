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

        TestAssert.Equal(121, sampleTimes.Count, "Large official runtime meshes should keep 30 FPS sampling when under the preview cap.");
    }

    public static void CreateSampleTimes_ShouldStayBounded_ForExtremeOfficialRuntimeMesh()
    {
        var budget = SpineRuntimeSamplingPolicy.CreateBudget(
            drawCommandCount: 1200,
            meshTriangleCount: 30000);

        var sampleTimes = SpineRuntimeSamplingPolicy.CreateSampleTimes(8f, budget);

        TestAssert.True(sampleTimes.Count <= 240, "Runtime sampling should stay within the hard frame cap.");
        TestAssert.True(sampleTimes.Count >= 2, "Runtime sampling should keep at least two frames for animated previews.");
    }
}
