using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extensions.Dtos;

namespace AssestBundleTools.Application.Extensions.Services;

/// <summary>
/// 提供受信任在线组件的目录查询和显式安装入口。
/// </summary>
public interface IOnlineComponentInstallationService
{
    IReadOnlyList<OnlineComponentDescriptorDto> GetCatalog();

    OnlineComponentDescriptorDto? ResolveDescriptor(string componentId);

    Task<Result<OnlineComponentInstallResultDto>> InstallAsync(
        string componentId,
        IProgress<OnlineComponentInstallProgressDto>? progress = null,
        CancellationToken cancellationToken = default);
}
