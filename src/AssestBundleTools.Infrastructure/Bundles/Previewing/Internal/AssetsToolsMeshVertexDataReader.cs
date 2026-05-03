using System.Buffers.Binary;
using AssetsTools.NET;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

internal static class AssetsToolsMeshVertexDataReader
{
    public static IReadOnlyList<AssetsToolsMeshChannelInfo> ReadChannels(AssetTypeValueField? vertexDataField)
    {
        var channelArrayField = AssetsToolsFieldReader.TryGetField(vertexDataField, "m_Channels", "Array");
        if (channelArrayField?.Children is not { Count: > 0 } children)
        {
            return Array.Empty<AssetsToolsMeshChannelInfo>();
        }

        var channels = new List<AssetsToolsMeshChannelInfo>(children.Count);
        for (var index = 0; index < children.Count; index++)
        {
            var channelField = children[index];
            var stream = AssetsToolsFieldReader.ReadByte(AssetsToolsFieldReader.TryGetField(channelField, "stream")) ?? 0;
            var offset = AssetsToolsFieldReader.ReadByte(AssetsToolsFieldReader.TryGetField(channelField, "offset")) ?? 0;
            var format = AssetsToolsFieldReader.ReadByte(AssetsToolsFieldReader.TryGetField(channelField, "format")) ?? 0;
            var dimension = AssetsToolsFieldReader.ReadByte(AssetsToolsFieldReader.TryGetField(channelField, "dimension")) ?? 0;
            channels.Add(new AssetsToolsMeshChannelInfo(
                Index: index,
                Stream: stream,
                Offset: offset,
                Format: format,
                Dimension: dimension,
                ComponentByteSize: ResolveComponentByteSize(format)));
        }

        return channels;
    }

    public static Dictionary<int, AssetsToolsMeshStreamLayout> BuildStreamLayouts(
        IReadOnlyList<AssetsToolsMeshChannelInfo> channels,
        int vertexCount)
    {
        var strides = new SortedDictionary<int, int>();
        foreach (var channel in channels)
        {
            if (channel.Dimension <= 0 || channel.ComponentByteSize <= 0)
            {
                continue;
            }

            var channelEndOffset = channel.Offset + (channel.ComponentByteSize * channel.Dimension);
            if (!strides.TryGetValue(channel.Stream, out var stride) || channelEndOffset > stride)
            {
                strides[channel.Stream] = channelEndOffset;
            }
        }

        var layouts = new Dictionary<int, AssetsToolsMeshStreamLayout>();
        var streamStartOffset = 0;
        foreach (var item in strides)
        {
            var stride = AlignToFourBytes(item.Value);
            layouts[item.Key] = new AssetsToolsMeshStreamLayout(streamStartOffset, stride);
            streamStartOffset = checked(streamStartOffset + (stride * vertexCount));
        }

        return layouts;
    }

    public static bool TryReadPosition(
        byte[] vertexBytes,
        AssetsToolsMeshStreamLayout streamLayout,
        AssetsToolsMeshChannelInfo positionChannel,
        int vertexIndex,
        out AssetPreviewMeshVertexDto vertex)
    {
        vertex = null!;

        var vertexOffset = streamLayout.StartOffset +
                           (vertexIndex * streamLayout.Stride) +
                           positionChannel.Offset;
        if (!TryReadFloatComponent(vertexBytes, vertexOffset, positionChannel.Format, out var x) ||
            !TryReadFloatComponent(vertexBytes, vertexOffset + positionChannel.ComponentByteSize, positionChannel.Format, out var y) ||
            !TryReadFloatComponent(vertexBytes, vertexOffset + (positionChannel.ComponentByteSize * 2), positionChannel.Format, out var z))
        {
            return false;
        }

        vertex = new AssetPreviewMeshVertexDto(x, y, z);
        return true;
    }

    public static IReadOnlyList<AssetPreviewMeshUvDto> ReadUvChannel(
        byte[] vertexBytes,
        IReadOnlyDictionary<int, AssetsToolsMeshStreamLayout> streamLayouts,
        IReadOnlyList<AssetsToolsMeshChannelInfo> channels,
        int channelIndex,
        int previewVertexCount)
    {
        if (channels.Count <= channelIndex)
        {
            return Array.Empty<AssetPreviewMeshUvDto>();
        }

        var uvChannel = channels[channelIndex];
        if (uvChannel.Dimension < 2 ||
            !uvChannel.CanReadAsFloat ||
            !streamLayouts.TryGetValue(uvChannel.Stream, out var uvStreamLayout))
        {
            return Array.Empty<AssetPreviewMeshUvDto>();
        }

        var uvs = new List<AssetPreviewMeshUvDto>(previewVertexCount);
        for (var vertexIndex = 0; vertexIndex < previewVertexCount; vertexIndex++)
        {
            var vertexOffset = uvStreamLayout.StartOffset +
                               (vertexIndex * uvStreamLayout.Stride) +
                               uvChannel.Offset;
            if (!TryReadFloatComponent(vertexBytes, vertexOffset, uvChannel.Format, out var u) ||
                !TryReadFloatComponent(vertexBytes, vertexOffset + uvChannel.ComponentByteSize, uvChannel.Format, out var v))
            {
                return Array.Empty<AssetPreviewMeshUvDto>();
            }

            uvs.Add(new AssetPreviewMeshUvDto(u, v));
        }

        return uvs;
    }

    private static bool TryReadFloatComponent(
        byte[] bytes,
        int offset,
        int format,
        out double value)
    {
        value = 0d;
        if (offset < 0 || offset >= bytes.Length)
        {
            return false;
        }

        switch (format)
        {
            case 0 when offset + sizeof(float) <= bytes.Length:
                value = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(offset, sizeof(float)));
                return IsFinite(value);
            case 1 when offset + sizeof(ushort) <= bytes.Length:
                value = (double)BitConverter.UInt16BitsToHalf(
                    BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort))));
                return IsFinite(value);
            default:
                return false;
        }
    }

    private static int ResolveComponentByteSize(int format)
    {
        return format switch
        {
            0 => 4,
            1 => 2,
            2 or 3 or 6 or 7 => 1,
            4 or 5 or 8 or 9 => 2,
            10 or 11 => 4,
            _ => 0
        };
    }

    private static int AlignToFourBytes(int value) =>
        value <= 0
            ? 0
            : ((value + 3) / 4) * 4;

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);
}

internal sealed record AssetsToolsMeshChannelInfo(
    int Index,
    int Stream,
    int Offset,
    int Format,
    int Dimension,
    int ComponentByteSize)
{
    public bool CanReadAsFloat => Format is 0 or 1 && ComponentByteSize > 0;
}

internal readonly record struct AssetsToolsMeshStreamLayout(
    int StartOffset,
    int Stride);
