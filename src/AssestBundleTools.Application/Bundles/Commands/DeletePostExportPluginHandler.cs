using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Bundles.Commands;

public sealed class DeletePostExportPluginHandler : ICommandHandler<DeletePostExportPluginCommand, Result>
{
    private readonly IPostExportPluginCatalogStore _catalogStore;

    public DeletePostExportPluginHandler(IPostExportPluginCatalogStore catalogStore)
    {
        _catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
    }

    public Task<Result> HandleAsync(DeletePostExportPluginCommand command, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result.Cancelled());
        }

        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.PluginId))
        {
            return Task.FromResult(Result.Failure(ApplicationErrors.PostExportPluginCatalogInvalid("Id")));
        }

        var pluginId = command.PluginId.Trim();
        var entry = _catalogStore.Load().FirstOrDefault(candidate =>
            string.Equals(candidate.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return Task.FromResult(Result.Failure(ApplicationErrors.PostExportPluginCatalogNotFound(pluginId)));
        }

        if (entry.IsReadOnly)
        {
            return Task.FromResult(Result.Failure(ApplicationErrors.PostExportPluginCatalogReadOnly(pluginId)));
        }

        try
        {
            _catalogStore.DeleteUserManagedPlugin(pluginId);
            return Task.FromResult(Result.Success());
        }
        catch (Exception exception)
        {
            return Task.FromResult(Result.Failure(
                ApplicationErrors.PostExportPluginCatalogDeleteFailed(exception.GetType().Name)));
        }
    }
}
