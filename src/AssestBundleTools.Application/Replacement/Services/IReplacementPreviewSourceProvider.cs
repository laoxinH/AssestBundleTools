using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;

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

    Task<Result<ReplacementTextChunkDto>> LoadFileTextChunkAsync(
        string sourceFilePath,
        int offset,
        int length,
        CancellationToken cancellationToken = default);

    Task<Result<ReplacementTextLineRangeDto>> LoadFileTextLinesAsync(
        string sourceFilePath,
        int startLineIndex,
        int lineCount,
        CancellationToken cancellationToken = default);

    Task<Result<ReplacementTextSearchResultDto>> SearchFileTextAsync(
        string sourceFilePath,
        string searchText,
        int startLineIndex,
        int maxMatchCount,
        bool matchCase,
        CancellationToken cancellationToken = default);
}
