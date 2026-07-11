using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using AssestBundleTools.BundleCodec.CrossCore;
using AssestBundleTools.Infrastructure.Bundles.Internal;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Decryption;

internal static class CrossCoreBundleCodecTests
{
    private const string RealSamplePath = @"C:\Users\thixi\Downloads\prefabs_spine_cg0085_twinstarattendantsb_spine";
    private const string PrefixedHeaderRealSamplePath = @"C:\Users\thixi\Downloads\prefabs_spine_10010_skin_alps06_spine";

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
        var metadata = CrossCoreBundleCodec.Decrypt(encryptedPath, roundTripPath);

        TestAssert.False(standardProbe.IsMatch, "Standard UnityFS bundle should not match the CrossCore codec.");
        TestAssert.True(encryptedProbe.IsMatch, "Duplicated-header UnityFS bundle should match the CrossCore codec.");
        TestAssert.Equal(100, encryptedProbe.Confidence, "Exact duplicated-header evidence should produce full confidence.");
        TestAssert.Equal(
            CrossCoreBundleCodec.DuplicatedHeaderFormatId,
            encryptedProbe.FormatId,
            "Probe should return the stable CrossCore format id.");
        TestAssert.True(
            File.ReadAllBytes(standardPath).SequenceEqual(File.ReadAllBytes(roundTripPath)),
            "Synthetic standard bundle should be byte-identical after encrypt/decrypt round-trip.");
        TestAssert.True(
            metadata.CodecParameters.ContainsKey(CrossCoreBundleCodec.OuterPrefixBase64Parameter),
            "Decrypt should retain the exact outer prefix for later export.");
    }

    public static void ProbeAndDecrypt_ShouldHandleNonDuplicatedOuterHeader()
    {
        using var scope = new TemporaryDirectoryScope();
        Directory.CreateDirectory(scope.DirectoryPath);
        var standardPath = Path.Combine(scope.DirectoryPath, "standard.bundle");
        var prefixedPath = Path.Combine(scope.DirectoryPath, "prefixed.bundle");
        var decodedPath = Path.Combine(scope.DirectoryPath, "decoded.bundle");
        var encodedPath = Path.Combine(scope.DirectoryPath, "encoded.bundle");
        WriteSyntheticStandardBundle(standardPath);

        var standardBytes = File.ReadAllBytes(standardPath);
        var outerHeader = standardBytes[..CrossCoreBundleCodec.DuplicatedPrefixLength];
        outerHeader[32] ^= 0x5A;
        File.WriteAllBytes(prefixedPath, [.. outerHeader, .. standardBytes]);

        var probe = CrossCoreBundleCodec.Probe(prefixedPath);
        var metadata = CrossCoreBundleCodec.Decrypt(prefixedPath, decodedPath);
        CrossCoreBundleCodec.Encrypt(
            decodedPath,
            encodedPath,
            metadata.CodecParameters[CrossCoreBundleCodec.OuterPrefixBase64Parameter]);

        TestAssert.True(probe.IsMatch, "A structurally valid non-duplicated CrossCore outer header should match.");
        TestAssert.Equal(
            CrossCoreBundleCodec.PrefixedHeaderFormatId,
            probe.FormatId,
            "A non-duplicated outer header should report the v2 format id.");
        TestAssert.True(
            standardBytes.SequenceEqual(File.ReadAllBytes(decodedPath)),
            "Decrypt should start at the structurally valid inner UnityFS header.");
        TestAssert.Equal(
            ComputeSha256(prefixedPath),
            ComputeSha256(encodedPath),
            "Encrypt should restore the exact non-duplicated outer prefix.");
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
        var metadata = CrossCoreBundleCodec.Decrypt(RealSamplePath, decodedPath);
        var decodedProbe = CrossCoreBundleCodec.Probe(decodedPath);
        CrossCoreBundleCodec.Encrypt(
            decodedPath,
            encodedPath,
            metadata.CodecParameters[CrossCoreBundleCodec.OuterPrefixBase64Parameter]);
        CrossCoreBundleCodec.Decrypt(encodedPath, roundTripPath);
        using var loadedSession = AssetsToolsBundleLoadService.LoadIsolated(decodedPath);

        TestAssert.True(probe.IsMatch, "Real CrossCore sample should match the built-in codec.");
        TestAssert.True(
            probe.Evidence.Contains("unityfs.inner-header.offset-83", StringComparer.Ordinal),
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

    public static void ProbeAndRoundTrip_ShouldHandlePrefixedHeaderRealSample_WhenAvailable()
    {
        if (!File.Exists(PrefixedHeaderRealSamplePath))
        {
            return;
        }

        using var scope = new TemporaryDirectoryScope();
        var decodedPath = Path.Combine(scope.DirectoryPath, "decoded-v2.bundle");
        var encodedPath = Path.Combine(scope.DirectoryPath, "encoded-v2.bundle");
        var roundTripPath = Path.Combine(scope.DirectoryPath, "roundtrip-v2.bundle");

        var probe = CrossCoreBundleCodec.Probe(PrefixedHeaderRealSamplePath);
        var metadata = CrossCoreBundleCodec.Decrypt(PrefixedHeaderRealSamplePath, decodedPath);
        using var loadedSession = AssetsToolsBundleLoadService.LoadIsolated(decodedPath);
        CrossCoreBundleCodec.Encrypt(
            decodedPath,
            encodedPath,
            metadata.CodecParameters[CrossCoreBundleCodec.OuterPrefixBase64Parameter]);
        var encodedProbe = CrossCoreBundleCodec.Probe(encodedPath);
        CrossCoreBundleCodec.Decrypt(encodedPath, roundTripPath);

        TestAssert.True(probe.IsMatch, "The non-duplicated CrossCore real sample should match the built-in codec.");
        TestAssert.Equal(
            CrossCoreBundleCodec.PrefixedHeaderFormatId,
            probe.FormatId,
            "The non-duplicated real sample should report the v2 format id.");
        TestAssert.True(
            loadedSession.SerializedFiles.Count > 0,
            "The normalized v2 real sample should be readable through AssetsTools.");
        TestAssert.Equal(
            CrossCoreBundleCodec.PrefixedHeaderFormatId,
            encodedProbe.FormatId,
            "Export encoding should preserve the source v2 prefix format.");
        TestAssert.Equal(
            ComputeSha256(PrefixedHeaderRealSamplePath),
            ComputeSha256(encodedPath),
            "Re-encoded v2 sample should be byte-identical to the original encrypted file.");
        TestAssert.Equal(
            ComputeSha256(decodedPath),
            ComputeSha256(roundTripPath),
            "The v2 normalized bundle should survive canonical encrypt/decrypt round-trip.");
    }

    private static void WriteSyntheticStandardBundle(string outputPath)
    {
        var data = Enumerable.Range(0, 4096)
            .Select(static index => (byte)((index * 37 + 11) & 0xFF))
            .ToArray();
        "UnityFS\0"u8.CopyTo(data);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8, 4), 8);
        Encoding.ASCII.GetBytes("5.x.x\0").CopyTo(data, 12);
        Encoding.ASCII.GetBytes("2022.3.62f2c1\0").CopyTo(data, 18);
        BinaryPrimitives.WriteInt64BigEndian(data.AsSpan(32, 8), data.Length);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(40, 4), 64);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(44, 4), 128);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(48, 4), 0x80);
        File.WriteAllBytes(outputPath, data);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
