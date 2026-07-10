using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using AssestBundleTools.BundleCodec.Arknights;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Decryption;

internal static class ArknightsBundleCodecTests
{
    private const string RealLz4AkSamplePath = @"D:\project\测试ab\char_2014_nian_cfa#1.ab";

    public static void EncryptAndDecrypt_ShouldRoundTripSyntheticUnityFsBundle()
    {
        using var scope = new TemporaryDirectoryScope();
        var standardPath = Path.Combine(scope.DirectoryPath, "standard.bundle");
        var encryptedPath = Path.Combine(scope.DirectoryPath, "encrypted.bundle");
        var roundTripPath = Path.Combine(scope.DirectoryPath, "roundtrip.bundle");
        Directory.CreateDirectory(scope.DirectoryPath);
        WriteSyntheticStandardBundle(standardPath);

        var standardProbe = ArknightsBundleCodec.Probe(standardPath);
        ArknightsBundleCodec.Encrypt(standardPath, encryptedPath);
        var encryptedProbe = ArknightsBundleCodec.Probe(encryptedPath);
        ArknightsBundleCodec.Decrypt(encryptedPath, roundTripPath);

        TestAssert.False(standardProbe.IsMatch, "Standard UnityFS bundle should not match the LZ4AK codec.");
        TestAssert.True(encryptedProbe.IsMatch, "Encoded UnityFS bundle should match the LZ4AK codec.");
        TestAssert.Equal(100, encryptedProbe.Confidence, "Explicit type-4 blocks should produce full confidence.");
        TestAssert.Equal(
            ArknightsBundleCodec.FormatId,
            encryptedProbe.FormatId,
            "Probe should return the stable Arknights format id.");
        TestAssert.True(
            File.ReadAllBytes(standardPath).SequenceEqual(File.ReadAllBytes(roundTripPath)),
            "Synthetic standard bundle should be byte-identical after encrypt/decrypt round-trip.");
    }

    public static void ProbeAndRoundTrip_ShouldHandleRealLz4AkSample_WhenAvailable()
    {
        if (!File.Exists(RealLz4AkSamplePath))
        {
            return;
        }

        using var scope = new TemporaryDirectoryScope();
        var decodedPath = Path.Combine(scope.DirectoryPath, "decoded.bundle");
        var encodedPath = Path.Combine(scope.DirectoryPath, "encoded.bundle");
        var roundTripPath = Path.Combine(scope.DirectoryPath, "roundtrip.bundle");

        var probe = ArknightsBundleCodec.Probe(RealLz4AkSamplePath);
        ArknightsBundleCodec.Decrypt(RealLz4AkSamplePath, decodedPath);
        ArknightsBundleCodec.Encrypt(decodedPath, encodedPath);
        ArknightsBundleCodec.Decrypt(encodedPath, roundTripPath);

        TestAssert.True(probe.IsMatch, "Real type-4 Arknights sample should match the built-in codec.");
        TestAssert.True(
            probe.Evidence.Contains("unityfs.data-block.type-4-or-5", StringComparer.Ordinal),
            "Real sample probe should retain its type-4 evidence.");
        TestAssert.Equal(
            ComputeSha256(decodedPath),
            ComputeSha256(roundTripPath),
            "Real sample normalized bundle should survive encrypt/decrypt round-trip.");
    }

    private static void WriteSyntheticStandardBundle(string outputPath)
    {
        var payload = Enumerable.Range(0, 64 * 1024)
            .Select(static index => (byte)((index * 31 + 17) & 0xFF))
            .ToArray();
        var blocksInfo = BuildBlocksInfo(payload.Length);
        var header = BuildHeader(totalSize: 0, blocksInfo.Length);
        header = BuildHeader(checked(header.Length + blocksInfo.Length + payload.Length), blocksInfo.Length);

        using var stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(header);
        stream.Write(blocksInfo);
        stream.Write(payload);
    }

    private static byte[] BuildBlocksInfo(int payloadLength)
    {
        using var stream = new MemoryStream();
        stream.Write(new byte[16]);
        WriteInt32(stream, 1);
        WriteUInt32(stream, (uint)payloadLength);
        WriteUInt32(stream, (uint)payloadLength);
        WriteUInt16(stream, 0);
        WriteInt32(stream, 1);
        WriteInt64(stream, 0);
        WriteInt64(stream, payloadLength);
        WriteUInt32(stream, 0);
        WriteCString(stream, "CAB-abt-codec-test");
        return stream.ToArray();
    }

    private static byte[] BuildHeader(long totalSize, int blocksInfoLength)
    {
        using var stream = new MemoryStream();
        WriteCString(stream, "UnityFS");
        WriteInt32(stream, 8);
        WriteCString(stream, "5.x.x");
        WriteCString(stream, "2021.3.39f1");
        WriteInt64(stream, totalSize);
        WriteUInt32(stream, (uint)blocksInfoLength);
        WriteUInt32(stream, (uint)blocksInfoLength);
        WriteUInt32(stream, 0x40);
        var alignedLength = (stream.Length + 15) & ~15;
        stream.Write(new byte[checked((int)(alignedLength - stream.Length))]);
        return stream.ToArray();
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void WriteCString(Stream stream, string value)
    {
        stream.Write(Encoding.ASCII.GetBytes(value));
        stream.WriteByte(0);
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteInt64(Stream stream, long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        stream.Write(buffer);
    }
}
