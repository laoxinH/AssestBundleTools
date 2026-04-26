using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Bundles.Services;

/// <summary>
/// 定义资源包解密器的统一扩展点。
/// </summary>
/// <remarks>
/// 解密器只负责把“原始导入来源”转换为“后续 Reader 可读取的资源包文件”，
/// 不承担真实 AB 解析、缓存写入或 UI 交互职责。
/// </remarks>
public interface IAssetBundleDecryptor
{
    AssetBundleDecryptorDescriptorDto Descriptor { get; }

    Task<AssetBundleDecryptorProbeResultDto> ProbeAsync(
        AssetBundleDecryptorProbeRequestDto request,
        CancellationToken cancellationToken = default);

    Task<Result<AssetBundleDecryptionResultDto>> DecryptAsync(
        AssetBundleDecryptionRequestDto request,
        CancellationToken cancellationToken = default);
}
