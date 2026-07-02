using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Queries;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 将导出资源处理器目录查询处理器适配为 Presentation 服务。
/// </summary>
public sealed class ResourceProcessorCatalogService : IResourceProcessorCatalogService
{
    private readonly GetResourceProcessorCatalogEntriesHandler _getEntriesHandler;

    public ResourceProcessorCatalogService(GetResourceProcessorCatalogEntriesHandler getEntriesHandler)
    {
        _getEntriesHandler = getEntriesHandler ?? throw new ArgumentNullException(nameof(getEntriesHandler));
    }

    public Task<Result<IReadOnlyList<ResourceProcessorCatalogEntryDto>>> GetEntriesAsync(
        CancellationToken cancellationToken = default)
    {
        return _getEntriesHandler.HandleAsync(new GetResourceProcessorCatalogEntriesQuery(), cancellationToken);
    }
}
