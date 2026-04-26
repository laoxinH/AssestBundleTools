using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Bundles.Queries;

/// <summary>
/// 读取解密器目录项快照。
/// </summary>
public sealed class GetAssetBundleDecryptorCatalogEntriesHandler
    : IQueryHandler<GetAssetBundleDecryptorCatalogEntriesQuery, Result<IReadOnlyList<AssetBundleDecryptorCatalogEntryDto>>>
{
    private readonly IAssetBundleDecryptorCatalogStore _catalogStore;

    public GetAssetBundleDecryptorCatalogEntriesHandler(IAssetBundleDecryptorCatalogStore catalogStore)
    {
        _catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
    }

    public Task<Result<IReadOnlyList<AssetBundleDecryptorCatalogEntryDto>>> HandleAsync(
        GetAssetBundleDecryptorCatalogEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<IReadOnlyList<AssetBundleDecryptorCatalogEntryDto>>.Cancelled());
        }

        return Task.FromResult(Result<IReadOnlyList<AssetBundleDecryptorCatalogEntryDto>>.Success(_catalogStore.Load()));
    }
}
