using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Bundles.Commands;

/// <summary>
/// 处理用户导出资源处理器定义的删除。
/// </summary>
public sealed class DeleteResourceProcessorHandler
    : ICommandHandler<DeleteResourceProcessorCommand, Result>
{
    private readonly IResourceProcessorCatalogStore _catalogStore;

    public DeleteResourceProcessorHandler(IResourceProcessorCatalogStore catalogStore)
    {
        _catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
    }

    public Task<Result> HandleAsync(
        DeleteResourceProcessorCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result.Cancelled());
        }

        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.ProcessorId))
        {
            return Task.FromResult(Result.Failure(ApplicationErrors.ResourceProcessorCatalogIdRequired()));
        }

        var processorId = command.ProcessorId.Trim();
        var existingEntry = _catalogStore.Load().FirstOrDefault(entry =>
            string.Equals(entry.Id, processorId, StringComparison.OrdinalIgnoreCase));
        if (existingEntry is null)
        {
            return Task.FromResult(Result.Failure(
                ApplicationErrors.ResourceProcessorCatalogNotFound(processorId)));
        }

        if (existingEntry.IsReadOnly)
        {
            return Task.FromResult(Result.Failure(
                ApplicationErrors.ResourceProcessorCatalogReadOnly(existingEntry.Id)));
        }

        try
        {
            _catalogStore.DeleteUserManagedProcessor(processorId);
            return Task.FromResult(Result.Success());
        }
        catch (Exception)
        {
            return Task.FromResult(Result.Failure(
                ApplicationErrors.ResourceProcessorCatalogDeleteFailed()));
        }
    }
}
