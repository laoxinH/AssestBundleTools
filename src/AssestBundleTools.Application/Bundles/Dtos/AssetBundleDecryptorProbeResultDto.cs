namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示解密器对某个资源包来源的自动识别结果。
/// </summary>
public sealed record AssetBundleDecryptorProbeResultDto(
    bool IsSuccessful,
    bool IsMatch,
    int Confidence,
    string? Message);
