using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Queries;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Presentation.WinUI.Services;

public sealed class PostExportPluginCatalogService : IPostExportPluginCatalogService
{
    private readonly GetPostExportPluginCatalogEntriesHandler _getEntriesHandler;
    private readonly UpsertPostExportPluginHandler _upsertHandler;
    private readonly DeletePostExportPluginHandler _deleteHandler;

    public PostExportPluginCatalogService(
        GetPostExportPluginCatalogEntriesHandler getEntriesHandler,
        UpsertPostExportPluginHandler upsertHandler,
        DeletePostExportPluginHandler deleteHandler)
    {
        _getEntriesHandler = getEntriesHandler ?? throw new ArgumentNullException(nameof(getEntriesHandler));
        _upsertHandler = upsertHandler ?? throw new ArgumentNullException(nameof(upsertHandler));
        _deleteHandler = deleteHandler ?? throw new ArgumentNullException(nameof(deleteHandler));
    }

    public Task<Result<IReadOnlyList<PostExportPluginCatalogEntryDto>>> GetEntriesAsync(
        CancellationToken cancellationToken = default) =>
        _getEntriesHandler.HandleAsync(new GetPostExportPluginCatalogEntriesQuery(), cancellationToken);

    public Task<Result<PostExportPluginCatalogEntryDto>> UpsertAsync(
        UpsertPostExportPluginCommand command,
        CancellationToken cancellationToken = default) =>
        _upsertHandler.HandleAsync(command, cancellationToken);

    public Task<Result> DeleteAsync(
        DeletePostExportPluginCommand command,
        CancellationToken cancellationToken = default) =>
        _deleteHandler.HandleAsync(command, cancellationToken);
}
