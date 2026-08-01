using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Bundles.Commands;

public sealed class ImportPluginPackageHandler
    : ICommandHandler<ImportPluginPackageCommand, Result<PluginPackageImportResultDto>>
{
    private readonly IPluginPackageInstallationService _installationService;

    public ImportPluginPackageHandler(IPluginPackageInstallationService installationService)
    {
        _installationService = installationService ?? throw new ArgumentNullException(nameof(installationService));
    }

    public Task<Result<PluginPackageImportResultDto>> HandleAsync(
        ImportPluginPackageCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<PluginPackageImportResultDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(command.SourcePath))
        {
            return Task.FromResult(Result<PluginPackageImportResultDto>.Failure(
                ApplicationErrors.PluginPackageSourceInvalid("PathRequired")));
        }

        return _installationService.ImportAsync(
            command.SourcePath.Trim(),
            command.ReplaceExisting,
            cancellationToken);
    }
}
