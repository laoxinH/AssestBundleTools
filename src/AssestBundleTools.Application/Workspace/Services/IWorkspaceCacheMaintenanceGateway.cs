using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Queries;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 聚合工作区缓存治理相关用例，供 Presentation 设置与诊断页使用。
/// </summary>
public interface IWorkspaceCacheMaintenanceGateway
{
    Task<Result<BundleArtifactCacheUsageDto>> GetBundleArtifactCacheUsageAsync(
        GetBundleArtifactCacheUsageQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<BundleArtifactPruneResultDto>> PruneBundleArtifactCacheAsync(
        PruneBundleArtifactCacheCommand command,
        CancellationToken cancellationToken = default);
}
