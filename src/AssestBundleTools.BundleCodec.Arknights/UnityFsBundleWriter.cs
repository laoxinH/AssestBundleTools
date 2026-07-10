using System.Buffers.Binary;
using System.Text;

namespace AssestBundleTools.BundleCodec.Arknights;

internal static class UnityFsBundleWriter
{
    private const int OutputBlockSize = 128 * 1024;

    public static void WriteStandardBundle(Stream input, UnityFsLayout layout, string outputPath)
    {
        var standardBlocks = layout.Blocks
            .Select(static block => new UnityFsBlock(
                block.UncompressedSize,
                block.UncompressedSize,
                (ushort)(block.Flags & ~UnityFsBundleReader.CompressionMask)))
            .ToArray();
        var blocksInfo = BuildBlocksInfo(layout.BlocksInfo, standardBlocks);
        var flags = NormalizeFlags(layout.Header.Flags, UnityFsCompressionType.None, blocksInfoAtEnd: false);
        var headerLength = BuildHeader(layout.Header, 0, (uint)blocksInfo.Length, (uint)blocksInfo.Length, flags).Length;
        var dataLength = standardBlocks.Aggregate(0L, static (total, block) => checked(total + block.UncompressedSize));
        var totalSize = checked(headerLength + blocksInfo.Length + dataLength);
        var header = BuildHeader(
            layout.Header,
            totalSize,
            (uint)blocksInfo.Length,
            (uint)blocksInfo.Length,
            flags);

        EnsureOutputDirectory(outputPath);
        using var output = CreateOutput(outputPath);
        output.Write(header);
        output.Write(blocksInfo);
        WriteDecodedBlocks(input, layout, output);
    }

    public static void WriteLz4AkBundle(Stream input, UnityFsLayout layout, string outputPath)
    {
        EnsureOutputDirectory(outputPath);
        var spoolPath = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? Environment.CurrentDirectory,
            $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.blocks.tmp");
        try
        {
            IReadOnlyList<UnityFsBlock> outputBlocks;
            using (var spool = new FileStream(
                       spoolPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 128 * 1024,
                       FileOptions.SequentialScan))
            {
                outputBlocks = WriteEncodedBlocks(input, layout, spool);
            }

            var rawBlocksInfo = BuildBlocksInfo(layout.BlocksInfo, outputBlocks);
            var compressedBlocksInfo = Lz4AkBlockCodec.CompressStandard(rawBlocksInfo);
            var flags = NormalizeFlags(layout.Header.Flags, UnityFsCompressionType.Lz4Hc, blocksInfoAtEnd: true);
            var headerLength = BuildHeader(
                layout.Header,
                0,
                (uint)compressedBlocksInfo.Length,
                (uint)rawBlocksInfo.Length,
                flags).Length;
            var dataLength = new FileInfo(spoolPath).Length;
            var totalSize = checked(headerLength + dataLength + compressedBlocksInfo.Length);
            var header = BuildHeader(
                layout.Header,
                totalSize,
                (uint)compressedBlocksInfo.Length,
                (uint)rawBlocksInfo.Length,
                flags);

            using var output = CreateOutput(outputPath);
            output.Write(header);
            using (var spool = new FileStream(
                       spoolPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       bufferSize: 128 * 1024,
                       FileOptions.SequentialScan))
            {
                spool.CopyTo(output);
            }

            output.Write(compressedBlocksInfo);
        }
        finally
        {
            TryDeleteFile(spoolPath);
        }
    }

    private static void WriteDecodedBlocks(Stream input, UnityFsLayout layout, Stream output)
    {
        var sourceOffset = layout.DataOffset;
        foreach (var block in layout.Blocks)
        {
            var compressedData = UnityFsBundleReader.ReadBlock(input, sourceOffset, block.CompressedSize);
            var uncompressedData = Lz4AkBlockCodec.Decompress(
                compressedData,
                block.UncompressedSize,
                block.CompressionType,
                out _);
            output.Write(uncompressedData);
            sourceOffset = checked(sourceOffset + block.CompressedSize);
        }
    }

    private static IReadOnlyList<UnityFsBlock> WriteEncodedBlocks(
        Stream input,
        UnityFsLayout layout,
        Stream output)
    {
        var outputBlocks = new List<UnityFsBlock>();
        var sourceOffset = layout.DataOffset;
        foreach (var sourceBlock in layout.Blocks)
        {
            var compressedData = UnityFsBundleReader.ReadBlock(input, sourceOffset, sourceBlock.CompressedSize);
            var uncompressedData = Lz4AkBlockCodec.Decompress(
                compressedData,
                sourceBlock.UncompressedSize,
                sourceBlock.CompressionType,
                out _);
            for (var offset = 0; offset < uncompressedData.Length; offset += OutputBlockSize)
            {
                var length = Math.Min(OutputBlockSize, uncompressedData.Length - offset);
                var encodedData = Lz4AkBlockCodec.CompressLz4Ak(uncompressedData.AsSpan(offset, length));
                output.Write(encodedData);
                outputBlocks.Add(new UnityFsBlock(
                    (uint)length,
                    (uint)encodedData.Length,
                    (ushort)UnityFsCompressionType.Lz4Ak));
            }

            sourceOffset = checked(sourceOffset + sourceBlock.CompressedSize);
        }

        return outputBlocks;
    }

    private static byte[] BuildBlocksInfo(
        UnityFsBlocksInfo source,
        IReadOnlyList<UnityFsBlock> blocks)
    {
        using var stream = new MemoryStream();
        stream.Write(source.Hash);
        WriteInt32BigEndian(stream, blocks.Count);
        foreach (var block in blocks)
        {
            WriteUInt32BigEndian(stream, block.UncompressedSize);
            WriteUInt32BigEndian(stream, block.CompressedSize);
            WriteUInt16BigEndian(stream, block.Flags);
        }

        WriteInt32BigEndian(stream, source.Directories.Count);
        foreach (var directory in source.Directories)
        {
            WriteInt64BigEndian(stream, directory.Offset);
            WriteInt64BigEndian(stream, directory.Size);
            WriteUInt32BigEndian(stream, directory.Flags);
            WriteCString(stream, directory.Path);
        }

        return stream.ToArray();
    }

    private static byte[] BuildHeader(
        UnityFsHeader source,
        long totalFileSize,
        uint compressedBlocksInfoSize,
        uint uncompressedBlocksInfoSize,
        uint flags)
    {
        using var stream = new MemoryStream();
        WriteCString(stream, "UnityFS");
        WriteInt32BigEndian(stream, source.FormatVersion);
        WriteCString(stream, source.PlayerVersion);
        WriteCString(stream, source.EngineVersion);
        WriteInt64BigEndian(stream, totalFileSize);
        WriteUInt32BigEndian(stream, compressedBlocksInfoSize);
        WriteUInt32BigEndian(stream, uncompressedBlocksInfoSize);
        WriteUInt32BigEndian(stream, flags);
        if (source.FormatVersion >= 7)
        {
            var alignedLength = UnityFsBundleReader.Align16(stream.Length);
            if (alignedLength > stream.Length)
            {
                stream.Write(new byte[checked((int)(alignedLength - stream.Length))]);
            }
        }

        return stream.ToArray();
    }

    private static uint NormalizeFlags(
        uint sourceFlags,
        UnityFsCompressionType compressionType,
        bool blocksInfoAtEnd)
    {
        var flags = sourceFlags & ~(uint)(
            UnityFsBundleReader.CompressionMask |
            UnityFsBundleReader.FlagBlocksInfoAtEnd |
            UnityFsBundleReader.FlagPaddingAtStart);
        flags |= UnityFsBundleReader.FlagCombined | (uint)compressionType;
        if (blocksInfoAtEnd)
        {
            flags |= UnityFsBundleReader.FlagBlocksInfoAtEnd;
        }

        return flags;
    }

    private static FileStream CreateOutput(string path) =>
        new(
            Path.GetFullPath(path),
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 128 * 1024,
            FileOptions.SequentialScan);

    private static void EnsureOutputDirectory(string outputPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void WriteCString(Stream stream, string value)
    {
        stream.Write(Encoding.ASCII.GetBytes(value));
        stream.WriteByte(0);
    }

    private static void WriteUInt16BigEndian(Stream stream, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteInt32BigEndian(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteUInt32BigEndian(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteInt64BigEndian(Stream stream, long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        stream.Write(buffer);
    }
}
