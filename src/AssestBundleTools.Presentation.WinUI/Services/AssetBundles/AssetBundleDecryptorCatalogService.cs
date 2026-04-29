using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Queries;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 将解密器目录管理相关处理器聚合为 Presentation 可直接调用的门面。
/// </summary>
public sealed class AssetBundleDecryptorCatalogService : IAssetBundleDecryptorCatalogService
{
    private readonly GetAssetBundleDecryptorCatalogEntriesHandler _getEntriesHandler;
    private readonly UpsertAssetBundleDecryptorHandler _upsertHandler;
    private readonly DeleteAssetBundleDecryptorHandler _deleteHandler;
    private readonly Action? _onCatalogChanged;

    public AssetBundleDecryptorCatalogService(
        GetAssetBundleDecryptorCatalogEntriesHandler getEntriesHandler,
        UpsertAssetBundleDecryptorHandler upsertHandler,
        DeleteAssetBundleDecryptorHandler deleteHandler,
        Action? onCatalogChanged = null)
    {
        _getEntriesHandler = getEntriesHandler ?? throw new ArgumentNullException(nameof(getEntriesHandler));
        _upsertHandler = upsertHandler ?? throw new ArgumentNullException(nameof(upsertHandler));
        _deleteHandler = deleteHandler ?? throw new ArgumentNullException(nameof(deleteHandler));
        _onCatalogChanged = onCatalogChanged;
    }

    public Task<Result<IReadOnlyList<AssetBundleDecryptorCatalogEntryDto>>> GetEntriesAsync(
        CancellationToken cancellationToken = default)
    {
        return _getEntriesHandler.HandleAsync(new GetAssetBundleDecryptorCatalogEntriesQuery(), cancellationToken);
    }

    public async Task<Result<AssetBundleDecryptorCatalogEntryDto>> UpsertAsync(
        UpsertAssetBundleDecryptorCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _upsertHandler.HandleAsync(command, cancellationToken);
        if (result.IsSuccess)
        {
            _onCatalogChanged?.Invoke();
        }

        return result;
    }

    public async Task<Result> DeleteAsync(
        DeleteAssetBundleDecryptorCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _deleteHandler.HandleAsync(command, cancellationToken);
        if (result.IsSuccess)
        {
            _onCatalogChanged?.Invoke();
        }

        return result;
    }
}
