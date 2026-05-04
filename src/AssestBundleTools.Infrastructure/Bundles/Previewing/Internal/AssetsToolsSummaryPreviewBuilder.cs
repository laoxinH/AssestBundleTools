using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

internal static class AssetsToolsSummaryPreviewBuilder
{
    private const string TextContentType = "text/plain; charset=utf-8";

    public static AssetPreviewContentDto BuildSummaryPreview(
        AssetFileInfo assetInfo,
        AssetTypeValueField baseField,
        AssetClassID classId)
    {
        // 详情页已经统一展示通用身份信息，
        // 摘要预览这里只保留类型专属补充字段，避免和“名称 / 路径 / PathId / 大小”重复。
        var items = new List<AssetPreviewInfoItemDto>();

        switch (classId)
        {
            case AssetClassID.AudioClip:
                AddIntegerInfo(items, AssetPreviewInfoKey.AudioLoadType, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_LoadType")));
                AddIntegerInfo(items, AssetPreviewInfoKey.AudioChannels, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Channels")));
                AddFrequencyInfo(items, AssetPreviewInfoKey.AudioFrequency, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Frequency")));
                AddBitsInfo(items, AssetPreviewInfoKey.AudioBitsPerSample, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_BitsPerSample")));
                AddDurationInfo(items, AssetPreviewInfoKey.AudioDuration, AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(baseField, "m_Length")));
                AddIntegerInfo(items, AssetPreviewInfoKey.AudioCompressionFormatId, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_CompressionFormat")));
                AddByteSizeInfo(items, AssetPreviewInfoKey.AudioResourceStreamSize, AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(baseField, "m_Resource", "m_Size")));
                break;

            case AssetClassID.Mesh:
                AddIntegerInfo(items, AssetPreviewInfoKey.MeshSubMeshCount, CountArray(baseField, "m_SubMeshes"));
                AddIntegerInfo(items, AssetPreviewInfoKey.MeshBlendShapeCount, CountArray(baseField, "m_Shapes", "channels"));
                AddIntegerInfo(items, AssetPreviewInfoKey.MeshBoneNameHashCount, CountArray(baseField, "m_BoneNameHashes"));
                break;

            case AssetClassID.Material:
                AddIntegerInfo(items, AssetPreviewInfoKey.MaterialTexturePropertyCount, CountArray(baseField, "m_SavedProperties", "m_TexEnvs"));
                AddIntegerInfo(items, AssetPreviewInfoKey.MaterialFloatPropertyCount, CountArray(baseField, "m_SavedProperties", "m_Floats"));
                AddIntegerInfo(items, AssetPreviewInfoKey.MaterialColorPropertyCount, CountArray(baseField, "m_SavedProperties", "m_Colors"));
                break;

            case AssetClassID.GameObject:
                AddIntegerInfo(items, AssetPreviewInfoKey.GameObjectComponentCount, CountArray(baseField, "m_Component"));
                AddIntegerInfo(items, AssetPreviewInfoKey.GameObjectLayer, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Layer")));
                AddBooleanInfo(items, AssetPreviewInfoKey.GameObjectIsActive, AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(baseField, "m_IsActive")));
                break;

            case AssetClassID.MonoBehaviour:
                AddBooleanInfo(items, AssetPreviewInfoKey.MonoBehaviourEnabled, AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(baseField, "m_Enabled")));
                AddInteger64Info(items, AssetPreviewInfoKey.MonoBehaviourScriptPathId, AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(baseField, "m_Script", "m_PathID")));
                break;

            case AssetClassID.AnimationClip:
                AddDecimalInfo(items, AssetPreviewInfoKey.AnimationClipSampleRate, AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(baseField, "m_SampleRate")));
                AddBooleanInfo(items, AssetPreviewInfoKey.AnimationClipIsLegacy, AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(baseField, "m_Legacy")));
                AddIntegerInfo(items, AssetPreviewInfoKey.AnimationClipEventCount, CountArray(baseField, "m_Events"));
                break;

            case AssetClassID.AnimatorController:
                AddIntegerInfo(items, AssetPreviewInfoKey.AnimatorControllerLayerCount, CountArray(baseField, "m_AnimatorLayers"));
                AddIntegerInfo(items, AssetPreviewInfoKey.AnimatorControllerParameterCount, CountArray(baseField, "m_AnimatorParameters"));
                break;

            case AssetClassID.Font:
                AddDecimalInfo(items, AssetPreviewInfoKey.FontLineSpacing, AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(baseField, "m_LineSpacing")));
                AddIntegerInfo(items, AssetPreviewInfoKey.FontDefaultSize, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_DefaultSize")));
                AddIntegerInfo(items, AssetPreviewInfoKey.FontAsciiStartOffset, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_AsciiStartOffset")));
                break;

            case AssetClassID.SpriteAtlas:
                AddIntegerInfo(items, AssetPreviewInfoKey.SpriteAtlasPackedSpriteCount, CountArray(baseField, "m_PackedSprites"));
                break;

            case AssetClassID.Sprite:
                AddDecimalInfo(items, AssetPreviewInfoKey.SpritePixelsPerUnit, AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(baseField, "m_PixelsToUnits")));
                AddInteger64Info(items, AssetPreviewInfoKey.SpriteTexturePathId, AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(baseField, "m_RD", "texture", "m_PathID")));
                AddTextInfo(items, AssetPreviewInfoKey.SpriteTextureRect, AssetsToolsImagePreviewSupport.FormatRect(AssetsToolsFieldReader.TryGetField(baseField, "m_RD", "textureRect")));
                break;

            case AssetClassID.Texture2D:
                AddIntegerInfo(items, AssetPreviewInfoKey.TextureWidth, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Width")));
                AddIntegerInfo(items, AssetPreviewInfoKey.TextureHeight, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Height")));
                AddIntegerInfo(items, AssetPreviewInfoKey.TextureMipCount, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_MipCount")));
                break;

            default:
                AddIntegerInfo(items, AssetPreviewInfoKey.UnknownTypeId, assetInfo.TypeId);
                break;
        }

        return new AssetPreviewContentDto(
            Kind: AssetPreviewKind.Summary,
            ContentType: TextContentType,
            Content: Array.Empty<byte>(),
            PreviewInfoItems: items,
            PreviewMessage: new AssetPreviewMessageDto(AssetPreviewMessageKey.MetadataOnly),
            PreferredDisplaySizeInBytes: null);
    }

    private static int CountArray(AssetTypeValueField baseField, params string[] path)
    {
        var arrayPath = new string[path.Length + 1];
        path.CopyTo(arrayPath, 0);
        arrayPath[^1] = "Array";
        var arrayField = AssetsToolsFieldReader.TryGetField(baseField, arrayPath);
        return AssetsToolsFieldReader.CountChildren(arrayField);
    }

    private static void AddTextInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        string? value)
    {
        AssetsToolsImagePreviewSupport.AddTextInfo(items, key, value);
    }

    private static void AddIntegerInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        int? value)
    {
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, key, value);
    }

    private static void AddInteger64Info(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        long? value)
    {
        AssetsToolsImagePreviewSupport.AddInteger64Info(items, key, value);
    }

    private static void AddByteSizeInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        long? value)
    {
        AssetsToolsImagePreviewSupport.AddByteSizeInfo(items, key, value);
    }

    private static void AddDecimalInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        float? value)
    {
        AssetsToolsImagePreviewSupport.AddDecimalInfo(items, key, value);
    }

    private static void AddDurationInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        float? value)
    {
        AssetsToolsImagePreviewSupport.AddDurationInfo(items, key, value);
    }

    private static void AddFrequencyInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        int? value)
    {
        AssetsToolsImagePreviewSupport.AddFrequencyInfo(items, key, value);
    }

    private static void AddBitsInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        int? value)
    {
        AssetsToolsImagePreviewSupport.AddBitsInfo(items, key, value);
    }

    private static void AddBooleanInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        bool? value)
    {
        AssetsToolsImagePreviewSupport.AddBooleanInfo(items, key, value);
    }
}
