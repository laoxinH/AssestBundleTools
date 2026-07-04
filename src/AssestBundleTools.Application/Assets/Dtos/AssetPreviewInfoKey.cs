namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 定义资源预览链路可产出的结构化信息键。
/// </summary>
/// <remarks>
/// 这些键是跨 Application / Infrastructure / Presentation 共享的稳定语义，
/// 展示层会基于它们映射本地化标签和格式。
/// </remarks>
public enum AssetPreviewInfoKey
{
    ImageDimensions = 0,
    PreviewPixelEncoding = 1,
    PreviewOutputFormat = 2,
    SerializedObjectSize = 3,

    TextDisplayCharacterCount = 10,
    TextFullCharacterCount = 11,
    TextDisplayLineCount = 12,
    TextEstimatedFullLineCount = 13,
    TextDisplayTruncated = 14,

    TextureFormatId = 20,
    TextureMipCount = 21,
    TextureDecodedSize = 22,
    TextureOriginalDataSize = 23,
    TextureStreamDataSize = 24,
    TextureStreamDataPath = 25,
    TextureWidth = 26,
    TextureHeight = 27,

    SpriteTexturePathId = 40,
    SpritePixelsPerUnit = 41,
    SpriteTextureRect = 42,
    SpriteCropSize = 43,
    SpriteCropOrigin = 44,
    SpriteSourceTextureDimensions = 45,
    SpriteSourceTextureFormatId = 46,
    SpritePivot = 47,
    SpriteBorder = 48,

    AudioContainer = 60,
    AudioOriginalContainer = 61,
    AudioOutputContainer = 62,
    AudioTranscoder = 63,
    AudioTranscoderVersion = 64,
    AudioPayloadSource = 65,
    AudioPayloadFile = 66,
    AudioPayloadSize = 67,
    AudioLoadType = 68,
    AudioChannels = 69,
    AudioFrequency = 70,
    AudioBitsPerSample = 71,
    AudioDuration = 72,
    AudioCompressionFormatId = 73,
    AudioResourceStreamSize = 74,

    MeshSubMeshCount = 90,
    MeshBlendShapeCount = 91,
    MeshBoneNameHashCount = 92,
    MeshVertexCount = 93,
    MeshIndexCount = 94,
    MeshTriangleCount = 95,
    MeshBoundsCenter = 96,
    MeshBoundsExtents = 97,
    MeshPreviewVertexCount = 98,
    MeshPreviewEdgeCount = 99,
    MeshPreviewGeometryTruncated = 100,
    MeshPreviewUvCount = 101,
    MeshVertexChannelSummary = 102,

    MaterialTexturePropertyCount = 110,
    MaterialFloatPropertyCount = 111,
    MaterialColorPropertyCount = 112,

    GameObjectComponentCount = 130,
    GameObjectLayer = 131,
    GameObjectIsActive = 132,

    MonoBehaviourEnabled = 150,
    MonoBehaviourScriptPathId = 151,

    AnimationClipSampleRate = 170,
    AnimationClipIsLegacy = 171,
    AnimationClipEventCount = 172,

    AnimatorControllerLayerCount = 190,
    AnimatorControllerParameterCount = 191,
    AnimatorControllerStateCount = 192,
    AnimatorControllerTransitionCount = 193,
    AnimatorControllerReferenceCount = 194,

    FontLineSpacing = 210,
    FontDefaultSize = 211,
    FontAsciiStartOffset = 212,
    FontCharacterCount = 213,
    FontTexturePathId = 214,
    FontDataSize = 215,
    FontAscent = 216,

    SpriteAtlasPackedSpriteCount = 230,

    UnknownTypeId = 250
}
