namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示资源预览退回摘要模式的主要原因。
/// </summary>
public enum AssetPreviewFallbackReasonKind
{
    None = 0,
    MissingLocator = 1,
    DecryptorResolveFailed = 2,
    DecryptionFailed = 3,
    GenerationFailed = 4
}
