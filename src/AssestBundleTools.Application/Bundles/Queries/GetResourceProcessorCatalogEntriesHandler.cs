using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Bundles.Queries;

/// <summary>
/// 返回当前阶段可用于导出窗口和插件管理页展示的资源处理器目录。
/// </summary>
public sealed class GetResourceProcessorCatalogEntriesHandler
    : IQueryHandler<GetResourceProcessorCatalogEntriesQuery, Result<IReadOnlyList<ResourceProcessorCatalogEntryDto>>>
{
    private readonly IResourceProcessorCatalogStore _catalogStore;

    public GetResourceProcessorCatalogEntriesHandler(IResourceProcessorCatalogStore catalogStore)
    {
        _catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
    }

    public Task<Result<IReadOnlyList<ResourceProcessorCatalogEntryDto>>> HandleAsync(
        GetResourceProcessorCatalogEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<IReadOnlyList<ResourceProcessorCatalogEntryDto>>.Cancelled());
        }

        return Task.FromResult(Result<IReadOnlyList<ResourceProcessorCatalogEntryDto>>.Success(_catalogStore.Load()));
    }
}
