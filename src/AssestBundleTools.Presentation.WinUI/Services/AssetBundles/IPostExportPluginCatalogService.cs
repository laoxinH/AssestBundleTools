using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Presentation.WinUI.Services;

public interface IPostExportPluginCatalogService
{
    Task<Result<IReadOnlyList<PostExportPluginCatalogEntryDto>>> GetEntriesAsync(
        CancellationToken cancellationToken = default);

    Task<Result<PostExportPluginCatalogEntryDto>> UpsertAsync(
        UpsertPostExportPluginCommand command,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        DeletePostExportPluginCommand command,
        CancellationToken cancellationToken = default);
}
