using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Bundles.Commands;

/// <summary>
/// 处理导出资源处理器定义的新增与更新。
/// </summary>
public sealed class UpsertResourceProcessorHandler
    : ICommandHandler<UpsertResourceProcessorCommand, Result<ResourceProcessorCatalogEntryDto>>
{
    private readonly IResourceProcessorCatalogStore _catalogStore;

    public UpsertResourceProcessorHandler(IResourceProcessorCatalogStore catalogStore)
    {
        _catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
    }

    public Task<Result<ResourceProcessorCatalogEntryDto>> HandleAsync(
        UpsertResourceProcessorCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<ResourceProcessorCatalogEntryDto>.Cancelled());
        }

        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Definition);

        var validationResult = Validate(command.Definition);
        if (validationResult is not null)
        {
            return Task.FromResult(Result<ResourceProcessorCatalogEntryDto>.Failure(validationResult));
        }

        var existingEntries = _catalogStore.Load();
        if (!string.IsNullOrWhiteSpace(command.PreviousProcessorId))
        {
            var existingEntry = existingEntries.FirstOrDefault(entry =>
                string.Equals(entry.Id, command.PreviousProcessorId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (existingEntry is null)
            {
                return Task.FromResult(Result<ResourceProcessorCatalogEntryDto>.Failure(
                    ApplicationErrors.ResourceProcessorCatalogNotFound(command.PreviousProcessorId.Trim())));
            }

            if (existingEntry.IsReadOnly)
            {
                return Task.FromResult(Result<ResourceProcessorCatalogEntryDto>.Failure(
                    ApplicationErrors.ResourceProcessorCatalogReadOnly(existingEntry.Id)));
            }
        }

        var conflictingEntry = existingEntries.FirstOrDefault(entry =>
            string.Equals(entry.Id, command.Definition.Id.Trim(), StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entry.Id, command.PreviousProcessorId?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (conflictingEntry is not null)
        {
            return Task.FromResult(Result<ResourceProcessorCatalogEntryDto>.Failure(
                conflictingEntry.IsReadOnly
                    ? ApplicationErrors.ResourceProcessorCatalogReadOnly(conflictingEntry.Id)
                    : ApplicationErrors.ResourceProcessorCatalogAlreadyExists(conflictingEntry.Id)));
        }

        try
        {
            _catalogStore.SaveUserManagedProcessor(command.Definition, command.PreviousProcessorId);
            var savedEntry = _catalogStore.Load().FirstOrDefault(entry =>
                string.Equals(entry.Id, command.Definition.Id.Trim(), StringComparison.OrdinalIgnoreCase));
            if (savedEntry is null)
            {
                return Task.FromResult(Result<ResourceProcessorCatalogEntryDto>.Failure(
                    ApplicationErrors.ResourceProcessorCatalogSaveVerificationFailed(command.Definition.Id.Trim())));
            }

            return Task.FromResult(Result<ResourceProcessorCatalogEntryDto>.Success(savedEntry));
        }
        catch (Exception)
        {
            return Task.FromResult(Result<ResourceProcessorCatalogEntryDto>.Failure(
                ApplicationErrors.ResourceProcessorCatalogSaveFailed()));
        }
    }

    private static AssestBundleTools.Domain.Common.Errors.AppError? Validate(ResourceProcessorDefinitionDto definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            return ApplicationErrors.ResourceProcessorCatalogIdRequired();
        }

        if (string.IsNullOrWhiteSpace(definition.DisplayName))
        {
            return ApplicationErrors.ResourceProcessorCatalogDisplayNameRequired();
        }

        if (definition.ResourceKinds.Count == 0 ||
            definition.ResourceKinds.All(static resourceKind => string.IsNullOrWhiteSpace(resourceKind)))
        {
            return ApplicationErrors.ResourceProcessorCatalogResourceKindsRequired();
        }

        if (string.IsNullOrWhiteSpace(definition.ExecutablePath))
        {
            return ApplicationErrors.ResourceProcessorCatalogExecutablePathRequired();
        }

        if (definition.TimeoutMs <= 0)
        {
            return ApplicationErrors.ResourceProcessorCatalogTimeoutInvalid();
        }

        if (definition.ProcessArguments.Count == 0 ||
            definition.ProcessArguments.All(static argument => string.IsNullOrWhiteSpace(argument)))
        {
            return ApplicationErrors.ResourceProcessorCatalogProcessArgumentsRequired();
        }

        return null;
    }
}
