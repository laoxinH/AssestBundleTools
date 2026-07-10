using System.Security.Cryptography;
using System.Text;
using AssestBundleTools.BundleCodec.CrossCore;
using AssestBundleTools.Infrastructure.Bundles.Internal;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Decryption;

internal static class CrossCoreBundleCodecTests
{
    private const string RealSamplePath = @"C:\Users\thixi\Downloads\prefabs_spine_cg0085_twinstarattendantsb_spine";

    public static void EncryptAndDecrypt_ShouldRoundTripSyntheticUnityFsBundle()
    {
        using var scope = new TemporaryDirectoryScope();
        var standardPath = Path.Combine(scope.DirectoryPath, "standard.bundle");
        var encryptedPath = Path.Combine(scope.DirectoryPath, "encrypted.bundle");
        var roundTripPath = Path.Combine(scope.DirectoryPath, "roundtrip.bundle");
        Directory.CreateDirectory(scope.DirectoryPath);
        WriteSyntheticStandardBundle(standardPath);

        var standardProbe = CrossCoreBundleCodec.Probe(standardPath);
        CrossCoreBundleCodec.Encrypt(standardPath, encryptedPath);
        var encryptedProbe = CrossCoreBundleCodec.Probe(encryptedPath);
        CrossCoreBundleCodec.Decrypt(encryptedPath, roundTripPath);

        TestAssert.False(standardProbe.IsMatch, "Standard UnityFS bundle should not match the CrossCore codec.");
        TestAssert.True(encryptedProbe.IsMatch, "Duplicated-header UnityFS bundle should match the CrossCore codec.");
        TestAssert.Equal(100, encryptedProbe.Confidence, "Exact duplicated-header evidence should produce full confidence.");
        TestAssert.Equal(
            CrossCoreBundleCodec.FormatId,
            encryptedProbe.FormatId,
            "Probe should return the stable CrossCore format id.");
        TestAssert.True(
            File.ReadAllBytes(standardPath).SequenceEqual(File.ReadAllBytes(roundTripPath)),
            "Synthetic standard bundle should be byte-identical after encrypt/decrypt round-trip.");
    }

    public static void ProbeAndRoundTrip_ShouldHandleRealSample_WhenAvailable()
    {
        if (!File.Exists(RealSamplePath))
        {
            return;
        }

        using var scope = new TemporaryDirectoryScope();
        var decodedPath = Path.Combine(scope.DirectoryPath, "decoded.bundle");
        var encodedPath = Path.Combine(scope.DirectoryPath, "encoded.bundle");
        var roundTripPath = Path.Combine(scope.DirectoryPath, "roundtrip.bundle");

        var probe = CrossCoreBundleCodec.Probe(RealSamplePath);
        CrossCoreBundleCodec.Decrypt(RealSamplePath, decodedPath);
        var decodedProbe = CrossCoreBundleCodec.Probe(decodedPath);
        CrossCoreBundleCodec.Encrypt(decodedPath, encodedPath);
        CrossCoreBundleCodec.Decrypt(encodedPath, roundTripPath);
        using var loadedSession = AssetsToolsBundleLoadService.LoadIsolated(decodedPath);

        TestAssert.True(probe.IsMatch, "Real CrossCore sample should match the built-in codec.");
        TestAssert.True(
            probe.Evidence.Contains("unityfs.second-header.offset-83", StringComparer.Ordinal),
            "Real sample probe should retain the verified duplicated-header offset.");
        TestAssert.False(decodedProbe.IsMatch, "Normalized real sample should no longer match the CrossCore codec.");
        TestAssert.True(
            loadedSession.SerializedFiles.Count > 0,
            "Normalized real sample should be readable through the shared AssetsTools bundle loader.");
        TestAssert.Equal(
            new FileInfo(RealSamplePath).Length,
            new FileInfo(encodedPath).Length,
            "Re-encoded real sample should preserve the original file length.");
        TestAssert.Equal(
            ComputeSha256(RealSamplePath),
            ComputeSha256(encodedPath),
            "Re-encoded real sample should be byte-identical to the original encrypted file.");
        TestAssert.Equal(
            ComputeSha256(decodedPath),
            ComputeSha256(roundTripPath),
            "Real normalized bundle should survive encrypt/decrypt round-trip.");
    }

    private static void WriteSyntheticStandardBundle(string outputPath)
    {
        var data = Enumerable.Range(0, 4096)
            .Select(static index => (byte)((index * 37 + 11) & 0xFF))
            .ToArray();
        "UnityFS\0"u8.CopyTo(data);
        Encoding.ASCII.GetBytes("5.x.x\0").CopyTo(data, 12);
        Encoding.ASCII.GetBytes("2022.3.62f2c1\0").CopyTo(data, 18);
        File.WriteAllBytes(outputPath, data);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
