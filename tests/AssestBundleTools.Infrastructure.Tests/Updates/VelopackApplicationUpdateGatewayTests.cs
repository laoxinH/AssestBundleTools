using AssestBundleTools.Application.Updates.Models;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Updates;

namespace AssestBundleTools.Infrastructure.Tests.Updates;

internal static class VelopackApplicationUpdateGatewayTests
{
    public static void RuntimeInfo_ShouldReportUnmanaged_WhenRunningOutsideVelopackPackage()
    {
        var gateway = new VelopackApplicationUpdateGateway();

        TestAssert.Equal(
            ApplicationInstallMode.Unmanaged,
            gateway.RuntimeInfo.InstallMode,
            "A test-hosted assembly should not be treated as a managed release package.");
        TestAssert.Equal(
            false,
            gateway.RuntimeInfo.CanSelfUpdate,
            "An unmanaged test-hosted assembly must not attempt to replace its output directory.");
        TestAssert.True(
            !string.IsNullOrWhiteSpace(gateway.RuntimeInfo.CurrentVersion),
            "The update runtime must always expose a diagnostic current version.");
    }
}
