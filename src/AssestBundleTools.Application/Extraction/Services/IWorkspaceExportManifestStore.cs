using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;

namespace AssestBundleTools.Application.Extraction.Services;

/// <summary>
/// 持久化批量导出 manifest，并负责文件大小、哈希等外部文件元数据读取。
/// </summary>
public interface IWorkspaceExportManifestStore
{
    Task<Result<WorkspaceExportManifestSaveResultDto>> SaveAsync(
        WorkspaceExportManifestSaveRequestDto request,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceExportManifestDto>> LoadAsync(
        string manifestPath,
        CancellationToken cancellationToken = default);
}
