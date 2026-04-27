namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示资源预览缓存命中判定后的状态。
/// </summary>
public enum AssetPreviewCacheStatusKind
{
    Hit = 0,
    Missing = 1,
    IdentityChanged = 2,
    SourceFingerprintChanged = 3,
    GeneratorVersionChanged = 4,
    AudioTranscoderChanged = 5
}
