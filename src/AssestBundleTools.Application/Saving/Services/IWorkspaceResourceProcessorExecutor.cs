using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Dtos;

namespace AssestBundleTools.Application.Saving.Services;

public interface IWorkspaceResourceProcessorExecutor
{
    Task<Result<WorkspaceResourceProcessorExecutionResultDto>> ExecuteAsync(
        WorkspaceResourceProcessorWorkItemDto workItem,
        CancellationToken cancellationToken = default);
}
