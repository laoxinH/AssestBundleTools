namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示解密器自动识别时的输入上下文。
/// </summary>
public sealed record AssetBundleDecryptorProbeRequestDto(
    string SourcePath,
    IReadOnlyDictionary<string, string>? Hints = null);
