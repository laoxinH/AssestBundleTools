using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Presentation.WinUI.Services;

public interface IPluginPackageImportService
{
    Task<Result<PluginPackageImportResultDto>> ImportAsync(
        ImportPluginPackageCommand command,
        CancellationToken cancellationToken = default);
}
