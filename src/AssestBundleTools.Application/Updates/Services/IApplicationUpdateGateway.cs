using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Updates.Models;

namespace AssestBundleTools.Application.Updates.Services;

/// <summary>
/// 隔离应用更新框架，向上层提供稳定的检查、下载和切换版本语义。
/// </summary>
public interface IApplicationUpdateGateway
{
    ApplicationUpdateRuntimeInfo RuntimeInfo { get; }

    Task<Result<ApplicationUpdateInfo?>> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    Task<Result> DownloadUpdateAsync(
        string targetVersion,
        IProgress<ApplicationUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Result ApplyUpdateAndRestart(string targetVersion);
}
