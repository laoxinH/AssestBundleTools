using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Bundles.Commands;

public sealed class UpsertPostExportPluginHandler
    : ICommandHandler<UpsertPostExportPluginCommand, Result<PostExportPluginCatalogEntryDto>>
{
    private readonly IPostExportPluginCatalogStore _catalogStore;

    public UpsertPostExportPluginHandler(IPostExportPluginCatalogStore catalogStore)
    {
        _catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
    }

    public Task<Result<PostExportPluginCatalogEntryDto>> HandleAsync(
        UpsertPostExportPluginCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<PostExportPluginCatalogEntryDto>.Cancelled());
        }

        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Definition);
        var validationError = Validate(command.Definition);
        if (validationError is not null)
        {
            return Task.FromResult(Result<PostExportPluginCatalogEntryDto>.Failure(validationError));
        }

        var entries = _catalogStore.Load();
        var previousId = command.PreviousPluginId?.Trim();
        if (!string.IsNullOrWhiteSpace(previousId))
        {
            var previous = entries.FirstOrDefault(entry =>
                string.Equals(entry.Id, previousId, StringComparison.OrdinalIgnoreCase));
            if (previous is null)
            {
                return Failure(ApplicationErrors.PostExportPluginCatalogNotFound(previousId));
            }

            if (previous.IsReadOnly)
            {
                return Failure(ApplicationErrors.PostExportPluginCatalogReadOnly(previous.Id));
            }
        }

        var targetId = command.Definition.Id.Trim();
        var conflict = entries.FirstOrDefault(entry =>
            string.Equals(entry.Id, targetId, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entry.Id, previousId, StringComparison.OrdinalIgnoreCase));
        if (conflict is not null)
        {
            return Failure(conflict.IsReadOnly
                ? ApplicationErrors.PostExportPluginCatalogReadOnly(conflict.Id)
                : ApplicationErrors.PostExportPluginCatalogAlreadyExists(conflict.Id));
        }

        try
        {
            _catalogStore.SaveUserManagedPlugin(command.Definition, previousId);
            var saved = _catalogStore.Load().FirstOrDefault(entry =>
                string.Equals(entry.Id, targetId, StringComparison.OrdinalIgnoreCase));
            return saved is null
                ? Failure(ApplicationErrors.PostExportPluginCatalogSaveFailed("VerificationFailed"))
                : Task.FromResult(Result<PostExportPluginCatalogEntryDto>.Success(saved));
        }
        catch (Exception exception)
        {
            return Failure(ApplicationErrors.PostExportPluginCatalogSaveFailed(exception.GetType().Name));
        }
    }

    private static AssestBundleTools.Domain.Common.Errors.AppError? Validate(PostExportPluginDefinitionDto definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            return ApplicationErrors.PostExportPluginCatalogInvalid("Id");
        }

        if (string.IsNullOrWhiteSpace(definition.DisplayName))
        {
            return ApplicationErrors.PostExportPluginCatalogInvalid("DisplayName");
        }

        if (string.IsNullOrWhiteSpace(definition.ExecutablePath))
        {
            return ApplicationErrors.PostExportPluginCatalogInvalid("ExecutablePath");
        }

        if (definition.ProcessArguments.Count == 0 ||
            !definition.ProcessArguments.Any(static argument =>
                argument.Contains("{manifestPath}", StringComparison.Ordinal)))
        {
            return ApplicationErrors.PostExportPluginCatalogInvalid("ProcessArguments.ManifestPath");
        }

        return null;
    }

    private static Task<Result<PostExportPluginCatalogEntryDto>> Failure(
        AssestBundleTools.Domain.Common.Errors.AppError error) =>
        Task.FromResult(Result<PostExportPluginCatalogEntryDto>.Failure(error));
}
