using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Replacement.Services;

/// <summary>
/// 为修改阶段的有效预览读取替换来源载荷。
/// </summary>
public interface IReplacementPreviewSourceProvider
{
    Task<Result<AssetPreviewContentDto>> LoadFilePreviewAsync(
        AssetDetailsDto targetDetails,
        string sourceFilePath,
        CancellationToken cancellationToken = default);
}
