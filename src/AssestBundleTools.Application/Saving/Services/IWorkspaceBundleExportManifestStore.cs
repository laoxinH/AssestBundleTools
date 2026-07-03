using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Dtos;

namespace AssestBundleTools.Application.Saving.Services;

/// <summary>
/// 持久化 AB 导出结果清单。
/// </summary>
public interface IWorkspaceBundleExportManifestStore
{
    Task<Result<WorkspaceBundleExportManifestSaveResultDto>> SaveAsync(
        WorkspaceBundleExportManifestSaveRequestDto request,
        CancellationToken cancellationToken = default);
}
