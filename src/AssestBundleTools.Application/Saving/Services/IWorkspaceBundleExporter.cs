using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Dtos;

namespace AssestBundleTools.Application.Saving.Services;

/// <summary>
/// 写出单个资源包的应用层端口。
/// </summary>
/// <remarks>
/// 当前端口以标准化 artifact 为只读输入，消费对象级写入项并生成新的 AB 输出。
/// Presentation 只提交导出意图，不感知具体写包方式。
/// </remarks>
public interface IWorkspaceBundleExporter
{
    Task<Result<WorkspaceBundleExportResultDto>> ExportAsync(
        WorkspaceBundleExportRequestDto request,
        CancellationToken cancellationToken = default);
}
