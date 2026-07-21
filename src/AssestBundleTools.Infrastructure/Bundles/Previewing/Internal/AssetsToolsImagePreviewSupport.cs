using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

internal static class AssetsToolsImagePreviewSupport
{
    public const string PngContentType = "image/png";
    public const string PixelEncoding = "RGBA8";
    public const string ContainerEncoding = "PNG";

    public static bool TryDecodeTextureImage(
        AssetsFileInstance serializedFile,
        AssetTypeValueField baseField,
        out AssetsToolsDecodedTextureImage textureImage)
    {
        textureImage = default;

        try
        {
            var textureFile = TextureFile.ReadTextureFile(baseField);
            if (textureFile.m_Width <= 0 || textureFile.m_Height <= 0)
            {
                return false;
            }

            var pictureData = textureFile.FillPictureData(serializedFile);
            if (pictureData is not { Length: > 0 })
            {
                return false;
            }

            var decodedPixels = textureFile.DecodeTextureRaw(pictureData, useBgra: false);
            var expectedLength = checked((long)textureFile.m_Width * textureFile.m_Height * 4L);
            if (decodedPixels is not { Length: > 0 } || decodedPixels.Length < expectedLength || expectedLength > int.MaxValue)
            {
                return false;
            }

            if (decodedPixels.Length != expectedLength)
            {
                Array.Resize(ref decodedPixels, (int)expectedLength);
            }

            textureImage = new AssetsToolsDecodedTextureImage(textureFile.m_Width, textureFile.m_Height, decodedPixels);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool TryEncodeTexturePreview(
        AssetsToolsDecodedTextureImage textureImage,
        IReadOnlyList<AssetPreviewInfoItemDto> previewInfoItems,
        long? preferredDisplaySizeInBytes,
        AssetPreviewImageOverlayDto? imageOverlay,
        AssetPreviewImageFocusDto? imageFocus,
        out AssetPreviewContentDto previewContent)
    {
        previewContent = null!;

        try
        {
            previewContent = new AssetPreviewContentDto(
                Kind: AssetPreviewKind.Image,
                ContentType: PngContentType,
                Content: EncodeRgbaPng(textureImage.Width, textureImage.Height, textureImage.RgbaPixels),
                PreviewInfoItems: previewInfoItems,
                PreferredDisplaySizeInBytes: preferredDisplaySizeInBytes,
                ImageOverlay: imageOverlay,
                ImageFocus: imageFocus);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static byte[] EncodeRgbaPng(int width, int height, byte[] rgbaPixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(rgbaPixels);

        var expectedPixelByteLength = checked(width * height * 4);
        if (rgbaPixels.Length != expectedPixelByteLength)
        {
            throw new ArgumentException("RGBA payload length does not match texture dimensions.", nameof(rgbaPixels));
        }

        using var filteredImageDataStream = new MemoryStream(capacity: checked(height * ((width * 4) + 1)));
        var rowLength = width * 4;
        for (var row = 0; row < height; row++)
        {
            filteredImageDataStream.WriteByte(0);
            var sourceRow = height - row - 1;
            filteredImageDataStream.Write(rgbaPixels, sourceRow * rowLength, rowLength);
        }

        filteredImageDataStream.Position = 0;

        using var compressedImageDataStream = new MemoryStream();
        using (var zlibStream = new ZLibStream(compressedImageDataStream, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            filteredImageDataStream.CopyTo(zlibStream);
        }

        using var pngStream = new MemoryStream();
        pngStream.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        WritePngChunk(pngStream, "IHDR", BuildPngHeader(width, height));
        WritePngChunk(pngStream, "IDAT", compressedImageDataStream.ToArray());
        WritePngChunk(pngStream, "IEND", Array.Empty<byte>());
        return pngStream.ToArray();
    }

    public static IReadOnlyList<AssetPreviewInfoItemDto> BuildTexturePreviewInfoItems(
        AssetTypeValueField textureField,
        AssetsToolsDecodedTextureImage textureImage)
    {
        var completeImageSize = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(textureField, "m_CompleteImageSize"));
        var streamSize = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(textureField, "m_StreamData", "size"));
        var streamSizeValue = streamSize.GetValueOrDefault();
        var hasDedicatedStreamSize = streamSizeValue > 0;
        var hasStandaloneCompleteSize = completeImageSize.HasValue &&
                                        completeImageSize.Value > 0 &&
                                        (!hasDedicatedStreamSize || completeImageSize.Value != streamSizeValue);

        var items = new List<AssetPreviewInfoItemDto>();
        AddTextInfo(items, AssetPreviewInfoKey.ImageDimensions, FormatDimensions(textureImage.Width, textureImage.Height));
        AddTextInfo(items, AssetPreviewInfoKey.PreviewPixelEncoding, PixelEncoding);
        AddTextInfo(items, AssetPreviewInfoKey.PreviewOutputFormat, ContainerEncoding);
        AddIntegerInfo(items, AssetPreviewInfoKey.TextureFormatId, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(textureField, "m_TextureFormat")));
        AddIntegerInfo(items, AssetPreviewInfoKey.TextureMipCount, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(textureField, "m_MipCount")));
        AddByteSizeInfo(items, AssetPreviewInfoKey.TextureDecodedSize, textureImage.RgbaPixels.LongLength);
        AddByteSizeInfo(items, AssetPreviewInfoKey.TextureOriginalDataSize, hasStandaloneCompleteSize ? completeImageSize : null);
        AddByteSizeInfo(items, AssetPreviewInfoKey.TextureStreamDataSize, hasDedicatedStreamSize ? streamSize : null);
        AddTextInfo(items, AssetPreviewInfoKey.TextureStreamDataPath, AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(textureField, "m_StreamData", "path")));
        return items;
    }

    public static void AddTextInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.Text,
            TextValue: value.Trim()));
    }

    public static void AddIntegerInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        int? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.Integer,
            Int64Value: value.Value));
    }

    public static void AddInteger64Info(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        long? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.Integer,
            Int64Value: value.Value));
    }

    public static void AddByteSizeInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        long? value)
    {
        if (!value.HasValue || value.Value <= 0)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.ByteSize,
            Int64Value: value.Value));
    }

    public static void AddDecimalInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        float? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.Decimal,
            NumberValue: value.Value));
    }

    public static void AddDurationInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        float? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.DurationSeconds,
            NumberValue: value.Value));
    }

    public static void AddFrequencyInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        int? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.FrequencyHertz,
            Int64Value: value.Value));
    }

    public static void AddBitsInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        int? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.BitsPerSample,
            Int64Value: value.Value));
    }

    public static void AddBooleanInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        bool? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.Boolean,
            BooleanValue: value.Value));
    }

    public static string? FormatRect(AssetTypeValueField? rectField)
    {
        if (rectField is null)
        {
            return null;
        }

        var x = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(rectField, "x"));
        var y = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(rectField, "y"));
        var width = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(rectField, "width"));
        var height = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(rectField, "height"));
        if (!x.HasValue || !y.HasValue || !width.HasValue || !height.HasValue)
        {
            return null;
        }

        return FormattableString.Invariant(
            $"x={x.Value:0.###}, y={y.Value:0.###}, w={width.Value:0.###}, h={height.Value:0.###}");
    }

    public static string FormatDimensions(int width, int height) =>
        $"{width.ToString(CultureInfo.InvariantCulture)} x {height.ToString(CultureInfo.InvariantCulture)}";

    private static byte[] BuildPngHeader(int width, int height)
    {
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8;
        header[9] = 6;
        header[10] = 0;
        header[11] = 0;
        header[12] = 0;
        return header;
    }

    private static void WritePngChunk(Stream outputStream, string chunkType, byte[] chunkData)
    {
        var chunkTypeBytes = Encoding.ASCII.GetBytes(chunkType);
        var chunkLengthBytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(chunkLengthBytes, chunkData.Length);
        outputStream.Write(chunkLengthBytes);
        outputStream.Write(chunkTypeBytes);
        outputStream.Write(chunkData);

        var crcInput = new byte[chunkTypeBytes.Length + chunkData.Length];
        Buffer.BlockCopy(chunkTypeBytes, 0, crcInput, 0, chunkTypeBytes.Length);
        Buffer.BlockCopy(chunkData, 0, crcInput, chunkTypeBytes.Length, chunkData.Length);

        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, ComputeCrc32(crcInput));
        outputStream.Write(crcBytes);
    }

    private static uint ComputeCrc32(byte[] data)
    {
        const uint Polynomial = 0xEDB88320u;
        var crc = 0xFFFFFFFFu;

        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0
                    ? (crc >> 1) ^ Polynomial
                    : crc >> 1;
            }
        }

        return ~crc;
    }
}
