using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 聚合导出资源处理器目录查询能力。
/// </summary>
public interface IResourceProcessorCatalogService
{
    Task<Result<IReadOnlyList<ResourceProcessorCatalogEntryDto>>> GetEntriesAsync(
        CancellationToken cancellationToken = default);
}
