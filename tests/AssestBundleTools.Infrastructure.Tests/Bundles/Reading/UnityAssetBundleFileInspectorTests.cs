using System.Text;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Reading;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Reading;

internal static class UnityAssetBundleFileInspectorTests
{
    public static void CanTreatAsBundle_ShouldRejectPngContent_WithNormalizedBundleExtension()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        var bundlePath = Path.Combine(temporaryDirectory.DirectoryPath, "normalized.bundle");
        File.WriteAllBytes(bundlePath, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        TestAssert.False(
            UnityAssetBundleFileInspector.CanTreatAsBundle(bundlePath),
            "A normalized .bundle filename must not make PNG content readable as a Unity bundle.");
        var inspection = UnityAssetBundleFileInspector.Inspect(bundlePath);
        TestAssert.Equal(
            AssetBundleSourceInspectionKind.Undetermined,
            inspection.Kind,
            "A common non-bundle signature should remain eligible for decryptor probing.");
        TestAssert.False(
            inspection.CanUsePassThrough,
            "PNG content should be blocked only when auto-detection would fall back to pass-through.");
    }

    public static void CanTreatAsBundle_ShouldAcceptKnownUnityHeader_WithoutKnownExtension()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        var bundlePath = Path.Combine(temporaryDirectory.DirectoryPath, "bundle-without-extension");
        File.WriteAllBytes(bundlePath, Encoding.ASCII.GetBytes("UnityFS\0test-content"));

        TestAssert.True(
            UnityAssetBundleFileInspector.CanTreatAsBundle(bundlePath),
            "Known Unity content should remain readable without relying on a filename extension.");
    }
}
