namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 定义资源预览链路中的结构化消息键。
/// </summary>
/// <remarks>
/// 这些键用于描述“为什么这里只能看到摘要 / 当前状态是什么”，
/// 真正展示给用户的文案由 Presentation 层本地化后生成。
/// </remarks>
public enum AssetPreviewMessageKey
{
    None = 0,
    MetadataOnly = 1,
    MissingLocator = 2,
    DecryptorResolveFailed = 3,
    DecryptionFailed = 4,
    GenerationFailed = 5,
    AudioPayloadMissing = 20,
    AudioUnknownContainerUnsupported = 21,
    AudioUnsupportedContainer = 22,
    AudioFsb5RequiresExternalTranscode = 23,
    AudioTranscodeUnavailable = 24,
    AudioTranscodeFailed = 25
}
