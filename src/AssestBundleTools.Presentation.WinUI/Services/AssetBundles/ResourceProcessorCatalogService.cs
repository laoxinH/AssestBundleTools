using AssestBundleTools.Application.Bundles.Commands;
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
    private readonly UpsertResourceProcessorHandler _upsertHandler;
    private readonly DeleteResourceProcessorHandler _deleteHandler;

    public ResourceProcessorCatalogService(
        GetResourceProcessorCatalogEntriesHandler getEntriesHandler,
        UpsertResourceProcessorHandler upsertHandler,
        DeleteResourceProcessorHandler deleteHandler)
    {
        _getEntriesHandler = getEntriesHandler ?? throw new ArgumentNullException(nameof(getEntriesHandler));
        _upsertHandler = upsertHandler ?? throw new ArgumentNullException(nameof(upsertHandler));
        _deleteHandler = deleteHandler ?? throw new ArgumentNullException(nameof(deleteHandler));
    }

    public Task<Result<IReadOnlyList<ResourceProcessorCatalogEntryDto>>> GetEntriesAsync(
        CancellationToken cancellationToken = default)
    {
        return _getEntriesHandler.HandleAsync(new GetResourceProcessorCatalogEntriesQuery(), cancellationToken);
    }

    public Task<Result<ResourceProcessorCatalogEntryDto>> UpsertAsync(
        UpsertResourceProcessorCommand command,
        CancellationToken cancellationToken = default)
    {
        return _upsertHandler.HandleAsync(command, cancellationToken);
    }

    public Task<Result> DeleteAsync(
        DeleteResourceProcessorCommand command,
        CancellationToken cancellationToken = default)
    {
        return _deleteHandler.HandleAsync(command, cancellationToken);
    }
}
