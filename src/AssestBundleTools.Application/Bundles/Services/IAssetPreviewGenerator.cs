using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Bundles.Services;

/// <summary>
/// 定义真实资源预览内容的生成入口。
/// </summary>
/// <remarks>
/// 生成器只面对“已经解密且可读取”的标准资源包文件，
/// 不负责工作区缓存命中、失效清理或 UI 状态管理。
/// </remarks>
public interface IAssetPreviewGenerator
{
    string GeneratorVersion { get; }

    Task<Result<AssetPreviewContentDto>> GenerateAsync(
        AssetPreviewBuildRequestDto request,
        CancellationToken cancellationToken = default);
}
