using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace AssestBundleTools.Infrastructure.Bundles.Internal;

/// <summary>
/// 统一解析资源在界面中的首选展示元数据，避免 Reader、预览器各自维护不同口径。
/// </summary>
internal static class AssetsToolsAssetDisplayMetadataResolver
{
    public static long? ResolvePreferredDisplaySizeInBytes(
        AssetClassID classId,
        AssetTypeValueField? baseField)
    {
        if (baseField is null)
        {
            return null;
        }

        return classId switch
        {
            AssetClassID.Texture2D => ResolvePreferredTextureSizeInBytes(baseField),
            AssetClassID.AudioClip => ResolvePreferredAudioSizeInBytes(baseField),
            _ => null
        };
    }

    private static long? ResolvePreferredAudioSizeInBytes(AssetTypeValueField audioClipField)
    {
        var resourceSize = AssetsToolsFieldReader.ReadInt64(
            AssetsToolsFieldReader.TryGetField(audioClipField, "m_Resource", "m_Size"));
        var inlineDataSize = AssetsToolsFieldReader.ReadByteArrayLength(
            AssetsToolsFieldReader.TryGetField(audioClipField, "m_AudioData"));

        return SelectPreferredAudioSizeInBytes(resourceSize, inlineDataSize);
    }

    internal static long? SelectPreferredAudioSizeInBytes(long? resourceSize, long? inlineDataSize)
    {
        if (resourceSize is > 0)
        {
            return resourceSize;
        }

        return inlineDataSize is > 0 ? inlineDataSize : null;
    }

    private static long? ResolvePreferredTextureSizeInBytes(AssetTypeValueField textureField)
    {
        var streamSize = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(textureField, "m_StreamData", "size"));
        if (streamSize.HasValue && streamSize.Value > 0)
        {
            return streamSize.Value;
        }

        var completeImageSize = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(textureField, "m_CompleteImageSize"));
        return completeImageSize.HasValue && completeImageSize.Value > 0
            ? completeImageSize.Value
            : null;
    }
}
