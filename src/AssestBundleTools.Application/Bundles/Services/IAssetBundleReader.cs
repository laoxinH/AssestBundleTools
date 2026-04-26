using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Bundles.Services;

/// <summary>
/// 定义“已可读取资源包文件”的统一读取入口。
/// </summary>
/// <remarks>
/// Reader 只面对解密后的标准文件，不负责探测加密格式或运行外部脚本。
/// </remarks>
public interface IAssetBundleReader
{
    string CacheParserVersion { get; }

    bool CanRead(string bundlePath);

    bool SupportsCacheParserVersion(string parserVersion);

    Task<Result<AssetBundleReadResultDto>> ReadAsync(
        AssetBundleReadRequestDto request,
        CancellationToken cancellationToken = default);
}
