using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 聚合解密器目录管理相关的应用层处理器。
/// </summary>
public interface IAssetBundleDecryptorCatalogService
{
    Task<Result<IReadOnlyList<AssetBundleDecryptorCatalogEntryDto>>> GetEntriesAsync(
        CancellationToken cancellationToken = default);

    Task<Result<AssetBundleDecryptorCatalogEntryDto>> UpsertAsync(
        UpsertAssetBundleDecryptorCommand command,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        DeleteAssetBundleDecryptorCommand command,
        CancellationToken cancellationToken = default);
}
