using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Dtos;

namespace AssestBundleTools.Application.Saving.Services;

public interface IWorkspacePostExportPluginLauncher
{
    Task<Result<WorkspacePostExportPluginLaunchResultDto>> LaunchAsync(
        WorkspacePostExportPluginLaunchRequestDto request,
        CancellationToken cancellationToken = default);
}
