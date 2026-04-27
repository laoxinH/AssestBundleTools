using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Bundles.Commands;

/// <summary>
/// 处理解密器定义的新增与更新。
/// </summary>
public sealed class UpsertAssetBundleDecryptorHandler
    : ICommandHandler<UpsertAssetBundleDecryptorCommand, Result<AssetBundleDecryptorCatalogEntryDto>>
{
    private readonly IAssetBundleDecryptorCatalogStore _catalogStore;

    public UpsertAssetBundleDecryptorHandler(IAssetBundleDecryptorCatalogStore catalogStore)
    {
        _catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
    }

    public Task<Result<AssetBundleDecryptorCatalogEntryDto>> HandleAsync(
        UpsertAssetBundleDecryptorCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<AssetBundleDecryptorCatalogEntryDto>.Cancelled());
        }

        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Definition);

        var validationResult = Validate(command.Definition);
        if (validationResult is not null)
        {
            return Task.FromResult(Result<AssetBundleDecryptorCatalogEntryDto>.Failure(validationResult));
        }

        var existingEntries = _catalogStore.Load();
        if (!string.IsNullOrWhiteSpace(command.PreviousDecryptorId))
        {
            var existingEntry = existingEntries.FirstOrDefault(entry =>
                string.Equals(entry.Definition.Id, command.PreviousDecryptorId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (existingEntry is null)
            {
                return Task.FromResult(Result<AssetBundleDecryptorCatalogEntryDto>.Failure(
                    ApplicationErrors.BundleDecryptorCatalogNotFound(command.PreviousDecryptorId.Trim())));
            }

            if (existingEntry.IsReadOnly)
            {
                return Task.FromResult(Result<AssetBundleDecryptorCatalogEntryDto>.Failure(
                    ApplicationErrors.BundleDecryptorCatalogReadOnly(existingEntry.Definition.Id)));
            }
        }

        var conflictingEntry = existingEntries.FirstOrDefault(entry =>
            string.Equals(entry.Definition.Id, command.Definition.Id.Trim(), StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entry.Definition.Id, command.PreviousDecryptorId?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (conflictingEntry is not null)
        {
            return Task.FromResult(Result<AssetBundleDecryptorCatalogEntryDto>.Failure(
                conflictingEntry.IsReadOnly
                    ? ApplicationErrors.BundleDecryptorCatalogReadOnly(conflictingEntry.Definition.Id)
                    : ApplicationErrors.BundleDecryptorCatalogAlreadyExists(conflictingEntry.Definition.Id)));
        }

        try
        {
            _catalogStore.SaveUserManagedDecryptor(command.Definition, command.PreviousDecryptorId);
            var savedEntry = _catalogStore.Load().FirstOrDefault(entry =>
                string.Equals(entry.Definition.Id, command.Definition.Id.Trim(), StringComparison.OrdinalIgnoreCase));
            if (savedEntry is null)
            {
                return Task.FromResult(Result<AssetBundleDecryptorCatalogEntryDto>.Failure(
                    ApplicationErrors.BundleDecryptorCatalogSaveVerificationFailed(command.Definition.Id.Trim())));
            }

            return Task.FromResult(Result<AssetBundleDecryptorCatalogEntryDto>.Success(savedEntry));
        }
        catch (Exception)
        {
            return Task.FromResult(Result<AssetBundleDecryptorCatalogEntryDto>.Failure(
                ApplicationErrors.BundleDecryptorCatalogSaveFailed()));
        }
    }

    private static AssestBundleTools.Domain.Common.Errors.AppError? Validate(AssetBundleDecryptorDefinitionDto definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            return ApplicationErrors.BundleDecryptorCatalogIdRequired();
        }

        if (string.IsNullOrWhiteSpace(definition.DisplayName))
        {
            return ApplicationErrors.BundleDecryptorCatalogDisplayNameRequired();
        }

        if (string.IsNullOrWhiteSpace(definition.ExecutablePath))
        {
            return ApplicationErrors.BundleDecryptorCatalogExecutablePathRequired();
        }

        if (definition.TimeoutMs <= 0)
        {
            return ApplicationErrors.BundleDecryptorCatalogTimeoutInvalid();
        }

        if (definition.DecryptArguments.Count == 0 ||
            definition.DecryptArguments.All(static argument => string.IsNullOrWhiteSpace(argument)))
        {
            return ApplicationErrors.BundleDecryptorCatalogDecryptArgumentsRequired();
        }

        return null;
    }
}
