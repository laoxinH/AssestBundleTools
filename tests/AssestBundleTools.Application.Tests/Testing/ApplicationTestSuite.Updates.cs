using AssestBundleTools.Application.Updates.Errors;
using AssestBundleTools.Application.Updates.Models;

namespace AssestBundleTools.Application.Tests.Testing;

internal static partial class ApplicationTestSuite
{
    private static Task ApplicationUpdateProgress_ShouldCalculateBoundedPercentage()
    {
        var halfway = new ApplicationUpdateProgress(50, 100);
        var beyondTotal = new ApplicationUpdateProgress(150, 100);
        var indeterminate = new ApplicationUpdateProgress(0, null);

        TestAssert.Equal(50d, halfway.Percentage, "Update progress should expose a percentage.");
        TestAssert.Equal(100d, beyondTotal.Percentage, "Update progress should be bounded at 100 percent.");
        TestAssert.Equal<double?>(null, indeterminate.Percentage, "Unknown totals should remain indeterminate.");
        return Task.CompletedTask;
    }

    private static Task ApplicationUpdateErrors_ShouldExposeStableDiagnosticCodes()
    {
        TestAssert.Equal(
            "Update.Check.RateLimited",
            ApplicationUpdateErrors.RateLimited().Code,
            "Rate limiting should retain its stable diagnostic code.");
        TestAssert.Equal(
            "Update.Download.ChecksumMismatch",
            ApplicationUpdateErrors.ChecksumMismatch().Code,
            "Checksum failures should retain their stable diagnostic code.");
        TestAssert.Equal(
            "Update.Apply.OtherInstancesRunning",
            ApplicationUpdateErrors.OtherInstancesRunning().Code,
            "Other-instance blocking should retain its stable diagnostic code.");
        return Task.CompletedTask;
    }
}
