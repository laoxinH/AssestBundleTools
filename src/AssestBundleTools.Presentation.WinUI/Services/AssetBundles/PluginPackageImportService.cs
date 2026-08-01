using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Presentation.WinUI.Services;

public sealed class PluginPackageImportService : IPluginPackageImportService
{
    private readonly ImportPluginPackageHandler _handler;

    public PluginPackageImportService(ImportPluginPackageHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public Task<Result<PluginPackageImportResultDto>> ImportAsync(
        ImportPluginPackageCommand command,
        CancellationToken cancellationToken = default) =>
        _handler.HandleAsync(command, cancellationToken);
}
