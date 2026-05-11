using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;

namespace AssestBundleTools.Application.Extraction.Services;

/// <summary>
/// 定义资源提取 payload 的文件系统写出端口。
/// </summary>
public interface IAssetExtractionFileStore
{
    Task<Result<AssetExtractionWriteResultDto>> WriteAsync(
        AssetExtractionWriteRequestDto request,
        CancellationToken cancellationToken = default);
}
