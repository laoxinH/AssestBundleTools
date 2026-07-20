using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Bundles.Queries;

public sealed class GetPostExportPluginCatalogEntriesHandler
    : IQueryHandler<GetPostExportPluginCatalogEntriesQuery, Result<IReadOnlyList<PostExportPluginCatalogEntryDto>>>
{
    private readonly IPostExportPluginCatalogStore _catalogStore;

    public GetPostExportPluginCatalogEntriesHandler(IPostExportPluginCatalogStore catalogStore)
    {
        _catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
    }

    public Task<Result<IReadOnlyList<PostExportPluginCatalogEntryDto>>> HandleAsync(
        GetPostExportPluginCatalogEntriesQuery query,
        CancellationToken cancellationToken = default) =>
        cancellationToken.IsCancellationRequested
            ? Task.FromResult(Result<IReadOnlyList<PostExportPluginCatalogEntryDto>>.Cancelled())
            : Task.FromResult(Result<IReadOnlyList<PostExportPluginCatalogEntryDto>>.Success(_catalogStore.Load()));
}
