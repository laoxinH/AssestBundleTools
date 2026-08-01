using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Bundles.Services;

/// <summary>
/// 校验并安装由文件夹或 ZIP 承载的外部 CLI 插件包。
/// </summary>
public interface IPluginPackageInstallationService
{
    Task<Result<PluginPackageImportResultDto>> ImportAsync(
        string sourcePath,
        bool replaceExisting,
        CancellationToken cancellationToken = default);
}
