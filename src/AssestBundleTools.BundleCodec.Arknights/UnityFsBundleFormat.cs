using System.Text;

namespace AssestBundleTools.BundleCodec.Arknights;

internal enum UnityFsCompressionType
{
    None = 0,
    Lzma = 1,
    Lz4 = 2,
    Lz4Hc = 3,
    Lz4Ak = 4,
    Lz4AkAlternate = 5
}

internal sealed record UnityFsHeader(
    int FormatVersion,
    string PlayerVersion,
    string EngineVersion,
    long TotalFileSize,
    uint CompressedBlocksInfoSize,
    uint UncompressedBlocksInfoSize,
    uint Flags,
    long HeaderEndOffset);

internal sealed record UnityFsBlock(uint UncompressedSize, uint CompressedSize, ushort Flags)
{
    public UnityFsCompressionType CompressionType =>
        (UnityFsCompressionType)(Flags & UnityFsBundleReader.CompressionMask);
}

internal sealed record UnityFsDirectoryEntry(long Offset, long Size, uint Flags, string Path);

internal sealed record UnityFsBlocksInfo(
    byte[] Hash,
    IReadOnlyList<UnityFsBlock> Blocks,
    IReadOnlyList<UnityFsDirectoryEntry> Directories);

internal sealed record UnityFsLayout(
    UnityFsHeader Header,
    UnityFsBlocksInfo BlocksInfo,
    long DataOffset,
    bool BlocksInfoUsedLz4Ak)
{
    public IReadOnlyList<UnityFsBlock> Blocks => BlocksInfo.Blocks;
}

internal static class UnityFsBundleReader
{
    public const ushort CompressionMask = 0x3F;
    public const uint FlagCombined = 0x40;
    public const uint FlagBlocksInfoAtEnd = 0x80;
    public const uint FlagPaddingAtStart = 0x200;

    private const int MaximumHeaderStringLength = 4096;
    private const int MaximumBlockCount = 100_000;
    private const int MaximumDirectoryCount = 100_000;

    public static UnityFsLayout ReadLayout(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new InvalidDataException("UnityFS input must be readable and seekable.");
        }

        stream.Position = 0;
        var signature = ReadCString(stream);
        if (!string.Equals(signature, "UnityFS", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Input is not a UnityFS bundle.");
        }

        var formatVersion = ReadInt32BigEndian(stream);
        var playerVersion = ReadCString(stream);
        var engineVersion = ReadCString(stream);
        var totalFileSize = ReadInt64BigEndian(stream);
        var compressedBlocksInfoSize = ReadUInt32BigEndian(stream);
        var uncompressedBlocksInfoSize = ReadUInt32BigEndian(stream);
        var flags = ReadUInt32BigEndian(stream);
        var headerEndOffset = formatVersion >= 7 ? Align16(stream.Position) : stream.Position;
        if (totalFileSize <= 0 || totalFileSize > stream.Length)
        {
            throw new InvalidDataException("UnityFS total file size is outside the input stream.");
        }

        var header = new UnityFsHeader(
            formatVersion,
            playerVersion,
            engineVersion,
            totalFileSize,
            compressedBlocksInfoSize,
            uncompressedBlocksInfoSize,
            flags,
            headerEndOffset);
        var blocksInfoOffset = (flags & FlagBlocksInfoAtEnd) != 0
            ? checked(totalFileSize - compressedBlocksInfoSize)
            : headerEndOffset;
        var compressedBlocksInfo = ReadBlock(stream, blocksInfoOffset, compressedBlocksInfoSize);
        var blocksInfoCompression = (UnityFsCompressionType)(flags & CompressionMask);
        var blocksInfoData = Lz4AkBlockCodec.Decompress(
            compressedBlocksInfo,
            uncompressedBlocksInfoSize,
            blocksInfoCompression,
            out var usedLz4Ak);
        var blocksInfo = ParseBlocksInfo(blocksInfoData);
        var dataOffset = (flags & FlagBlocksInfoAtEnd) != 0
            ? headerEndOffset
            : checked(headerEndOffset + compressedBlocksInfoSize);
        if ((flags & FlagPaddingAtStart) != 0)
        {
            dataOffset = Align16(dataOffset);
        }

        ValidateDataRange(stream, header, blocksInfo, dataOffset, blocksInfoOffset);
        return new UnityFsLayout(header, blocksInfo, dataOffset, usedLz4Ak);
    }

    public static byte[] ReadBlock(Stream stream, long offset, uint size)
    {
        if (size > int.MaxValue)
        {
            throw new NotSupportedException("UnityFS block is too large for this codec process.");
        }

        if (offset < 0 || offset > stream.Length - size)
        {
            throw new InvalidDataException("UnityFS block range is outside the input stream.");
        }

        stream.Position = offset;
        var data = new byte[(int)size];
        stream.ReadExactly(data);
        return data;
    }

    public static long Align16(long value) => checked((value + 15L) & ~15L);

    private static UnityFsBlocksInfo ParseBlocksInfo(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        var hash = new byte[16];
        stream.ReadExactly(hash);
        var blockCount = ReadInt32BigEndian(stream);
        if (blockCount <= 0 || blockCount > MaximumBlockCount)
        {
            throw new InvalidDataException("UnityFS block count is invalid.");
        }

        var blocks = new UnityFsBlock[blockCount];
        for (var index = 0; index < blocks.Length; index++)
        {
            blocks[index] = new UnityFsBlock(
                ReadUInt32BigEndian(stream),
                ReadUInt32BigEndian(stream),
                ReadUInt16BigEndian(stream));
        }

        var directoryCount = ReadInt32BigEndian(stream);
        if (directoryCount < 0 || directoryCount > MaximumDirectoryCount)
        {
            throw new InvalidDataException("UnityFS directory count is invalid.");
        }

        var directories = new UnityFsDirectoryEntry[directoryCount];
        for (var index = 0; index < directories.Length; index++)
        {
            directories[index] = new UnityFsDirectoryEntry(
                ReadInt64BigEndian(stream),
                ReadInt64BigEndian(stream),
                ReadUInt32BigEndian(stream),
                ReadCString(stream));
        }

        if (stream.Position != stream.Length)
        {
            throw new InvalidDataException("UnityFS blocks-info contains unexpected trailing data.");
        }

        return new UnityFsBlocksInfo(hash, blocks, directories);
    }

    private static void ValidateDataRange(
        Stream stream,
        UnityFsHeader header,
        UnityFsBlocksInfo blocksInfo,
        long dataOffset,
        long blocksInfoOffset)
    {
        var compressedDataSize = blocksInfo.Blocks.Aggregate(
            0L,
            static (total, block) => checked(total + block.CompressedSize));
        var dataEnd = checked(dataOffset + compressedDataSize);
        var maximumDataEnd = (header.Flags & FlagBlocksInfoAtEnd) != 0
            ? blocksInfoOffset
            : header.TotalFileSize;
        if (dataOffset < header.HeaderEndOffset || dataEnd > maximumDataEnd || dataEnd > stream.Length)
        {
            throw new InvalidDataException("UnityFS data blocks exceed the declared file range.");
        }

        var uncompressedDataSize = blocksInfo.Blocks.Aggregate(
            0L,
            static (total, block) => checked(total + block.UncompressedSize));
        if (blocksInfo.Directories.Any(directory =>
                directory.Offset < 0 ||
                directory.Size < 0 ||
                directory.Offset > uncompressedDataSize - directory.Size))
        {
            throw new InvalidDataException("UnityFS directory range exceeds the uncompressed data.");
        }
    }

    private static string ReadCString(Stream stream)
    {
        using var buffer = new MemoryStream();
        while (buffer.Length <= MaximumHeaderStringLength)
        {
            var value = stream.ReadByte();
            if (value < 0)
            {
                throw new EndOfStreamException("UnityFS string is truncated.");
            }

            if (value == 0)
            {
                return Encoding.ASCII.GetString(buffer.ToArray());
            }

            buffer.WriteByte((byte)value);
        }

        throw new InvalidDataException("UnityFS string exceeds the supported length.");
    }

    private static ushort ReadUInt16BigEndian(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[2];
        stream.ReadExactly(buffer);
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(buffer);
    }

    private static int ReadInt32BigEndian(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[4];
        stream.ReadExactly(buffer);
        return System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(buffer);
    }

    private static uint ReadUInt32BigEndian(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[4];
        stream.ReadExactly(buffer);
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(buffer);
    }

    private static long ReadInt64BigEndian(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[8];
        stream.ReadExactly(buffer);
        return System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(buffer);
    }
}
