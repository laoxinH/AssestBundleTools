using K4os.Compression.LZ4;

namespace AssestBundleTools.BundleCodec.Arknights;

internal static class Lz4AkBlockCodec
{
    public static bool IsLz4AkType(UnityFsCompressionType compressionType) =>
        compressionType is UnityFsCompressionType.Lz4Ak or UnityFsCompressionType.Lz4AkAlternate;

    public static byte[] Decompress(
        byte[] compressedData,
        uint uncompressedSize,
        UnityFsCompressionType compressionType,
        out bool usedLz4Ak)
    {
        usedLz4Ak = false;
        var targetSize = ToBufferLength(uncompressedSize, "Uncompressed block");
        if (compressionType == UnityFsCompressionType.None)
        {
            if (compressedData.Length != targetSize)
            {
                throw new InvalidDataException("Uncompressed UnityFS block size is inconsistent.");
            }

            return compressedData;
        }

        if (compressionType is not (
            UnityFsCompressionType.Lz4 or
            UnityFsCompressionType.Lz4Hc or
            UnityFsCompressionType.Lz4Ak or
            UnityFsCompressionType.Lz4AkAlternate))
        {
            throw new NotSupportedException($"UnityFS compression type {(int)compressionType} is not supported.");
        }

        if (!IsLz4AkType(compressionType) && TryDecode(compressedData, targetSize, out var standardOutput))
        {
            return standardOutput;
        }

        var standardData = TransformLz4AkToStandard(compressedData, targetSize);
        if (!TryDecode(standardData, targetSize, out var lz4AkOutput))
        {
            throw new InvalidDataException("LZ4AK block could not be decoded to its declared size.");
        }

        usedLz4Ak = true;
        return lz4AkOutput;
    }

    public static byte[] CompressLz4Ak(ReadOnlySpan<byte> uncompressedData)
    {
        var maximumLength = LZ4Codec.MaximumOutputSize(uncompressedData.Length);
        var compressed = new byte[maximumLength];
        var written = LZ4Codec.Encode(uncompressedData, compressed, LZ4Level.L09_HC);
        if (written <= 0)
        {
            throw new InvalidDataException("Standard LZ4 encoder did not produce a block.");
        }

        return TransformStandardToLz4Ak(compressed.AsSpan(0, written));
    }

    public static byte[] CompressStandard(ReadOnlySpan<byte> uncompressedData)
    {
        var maximumLength = LZ4Codec.MaximumOutputSize(uncompressedData.Length);
        var compressed = new byte[maximumLength];
        var written = LZ4Codec.Encode(uncompressedData, compressed, LZ4Level.L09_HC);
        if (written <= 0)
        {
            throw new InvalidDataException("Standard LZ4 encoder did not produce a block.");
        }

        return compressed.AsSpan(0, written).ToArray();
    }

    public static bool CanDecodeStandard(byte[] compressedData, uint uncompressedSize) =>
        TryDecode(compressedData, ToBufferLength(uncompressedSize, "Uncompressed block"), out _);

    public static bool CanDecodeLz4Ak(byte[] compressedData, uint uncompressedSize)
    {
        try
        {
            var targetSize = ToBufferLength(uncompressedSize, "Uncompressed block");
            return TryDecode(TransformLz4AkToStandard(compressedData, targetSize), targetSize, out _);
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static byte[] TransformLz4AkToStandard(ReadOnlySpan<byte> input, int expectedSize) =>
        TransformSequences(input, expectedSize, inputUsesLz4AkLayout: true);

    private static byte[] TransformStandardToLz4Ak(ReadOnlySpan<byte> input) =>
        TransformSequences(input, expectedSize: null, inputUsesLz4AkLayout: false);

    private static byte[] TransformSequences(
        ReadOnlySpan<byte> input,
        int? expectedSize,
        bool inputUsesLz4AkLayout)
    {
        var output = input.ToArray();
        var inputPosition = 0;
        long outputPosition = 0;
        while (inputPosition < output.Length)
        {
            var token = output[inputPosition];
            var literalLength = inputUsesLz4AkLayout ? token & 0x0F : token >> 4;
            var matchLength = inputUsesLz4AkLayout ? token >> 4 : token & 0x0F;
            output[inputPosition] = (byte)((token << 4) | (token >> 4));
            inputPosition++;

            literalLength = ReadExtendedLength(output, ref inputPosition, literalLength);
            inputPosition = checked(inputPosition + literalLength);
            outputPosition += literalLength;
            if (inputPosition > output.Length)
            {
                throw new InvalidDataException("LZ4 literal range exceeds the compressed block.");
            }

            if (inputPosition == output.Length)
            {
                break;
            }

            if (inputPosition + 1 >= output.Length)
            {
                throw new InvalidDataException("LZ4 match offset is truncated.");
            }

            (output[inputPosition], output[inputPosition + 1]) =
                (output[inputPosition + 1], output[inputPosition]);
            inputPosition += 2;
            matchLength = ReadExtendedLength(output, ref inputPosition, matchLength);
            outputPosition += matchLength + 4L;

            if (expectedSize.HasValue && outputPosition > expectedSize.Value)
            {
                throw new InvalidDataException("LZ4AK sequence exceeds the declared output size.");
            }
        }

        if (expectedSize.HasValue && outputPosition != expectedSize.Value)
        {
            throw new InvalidDataException("LZ4AK sequence size does not match the declared output size.");
        }

        return output;
    }

    private static int ReadExtendedLength(byte[] data, ref int position, int length)
    {
        if (length != 0x0F)
        {
            return length;
        }

        byte next;
        do
        {
            if (position >= data.Length)
            {
                throw new InvalidDataException("LZ4 extended length is truncated.");
            }

            next = data[position++];
            length = checked(length + next);
        }
        while (next == byte.MaxValue);

        return length;
    }

    private static bool TryDecode(byte[] compressedData, int targetSize, out byte[] output)
    {
        output = new byte[targetSize];
        try
        {
            return LZ4Codec.Decode(
                       compressedData,
                       0,
                       compressedData.Length,
                       output,
                       0,
                       output.Length) == targetSize;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    private static int ToBufferLength(uint size, string label)
    {
        if (size > int.MaxValue)
        {
            throw new NotSupportedException($"{label} is too large for this codec process.");
        }

        return (int)size;
    }
}
