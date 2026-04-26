namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示某个解密器的静态描述信息。
/// </summary>
public sealed record AssetBundleDecryptorDescriptorDto(
    string Id,
    string DisplayName,
    string Version,
    AssetBundleDecryptorKind Kind,
    bool SupportsAutoDetection,
    bool IsFallback,
    string? Description);
