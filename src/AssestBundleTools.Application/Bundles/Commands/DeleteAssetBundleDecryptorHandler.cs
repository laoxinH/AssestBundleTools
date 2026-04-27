using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Bundles.Commands;

/// <summary>
/// 处理用户解密器定义的删除。
/// </summary>
public sealed class DeleteAssetBundleDecryptorHandler
    : ICommandHandler<DeleteAssetBundleDecryptorCommand, Result>
{
    private readonly IAssetBundleDecryptorCatalogStore _catalogStore;

    public DeleteAssetBundleDecryptorHandler(IAssetBundleDecryptorCatalogStore catalogStore)
    {
        _catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
    }

    public Task<Result> HandleAsync(
        DeleteAssetBundleDecryptorCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result.Cancelled());
        }

        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.DecryptorId))
        {
            return Task.FromResult(Result.Failure(ApplicationErrors.BundleDecryptorCatalogIdRequired()));
        }

        var decryptorId = command.DecryptorId.Trim();
        var existingEntry = _catalogStore.Load().FirstOrDefault(entry =>
            string.Equals(entry.Definition.Id, decryptorId, StringComparison.OrdinalIgnoreCase));
        if (existingEntry is null)
        {
            return Task.FromResult(Result.Failure(
                ApplicationErrors.BundleDecryptorCatalogNotFound(decryptorId)));
        }

        if (existingEntry.IsReadOnly)
        {
            return Task.FromResult(Result.Failure(
                ApplicationErrors.BundleDecryptorCatalogReadOnly(existingEntry.Definition.Id)));
        }

        try
        {
            _catalogStore.DeleteUserManagedDecryptor(decryptorId);
            return Task.FromResult(Result.Success());
        }
        catch (Exception)
        {
            return Task.FromResult(Result.Failure(
                ApplicationErrors.BundleDecryptorCatalogDeleteFailed()));
        }
    }
}
