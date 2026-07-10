using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Bundles.Services;

/// <summary>
/// 定义可选的资源包来源格式回写能力。
/// </summary>
public interface IAssetBundleEncryptor
{
    Task<Result<AssetBundleEncryptionResultDto>> EncryptAsync(
        AssetBundleEncryptionRequestDto request,
        CancellationToken cancellationToken = default);
}
