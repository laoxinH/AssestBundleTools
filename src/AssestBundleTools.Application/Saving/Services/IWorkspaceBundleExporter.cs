using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Dtos;

namespace AssestBundleTools.Application.Saving.Services;

/// <summary>
/// 写出单个资源包的应用层端口。
/// </summary>
/// <remarks>
/// 当前端口同时承载标准化 artifact 的文件级输出和后续 AssetsTools 对象级重建实现。
/// Presentation 只提交导出意图，不感知具体写包方式。
/// </remarks>
public interface IWorkspaceBundleExporter
{
    Task<Result<WorkspaceBundleExportResultDto>> ExportAsync(
        WorkspaceBundleExportRequestDto request,
        CancellationToken cancellationToken = default);
}
