using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Queries;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 聚合工作区缓存治理 Handler，避免 Presentation 直接感知具体用例实现。
/// </summary>
public sealed class WorkspaceCacheMaintenanceGateway : IWorkspaceCacheMaintenanceGateway
{
    private readonly GetBundleArtifactCacheUsageHandler _getBundleArtifactCacheUsageHandler;
    private readonly PruneBundleArtifactCacheHandler _pruneBundleArtifactCacheHandler;

    public WorkspaceCacheMaintenanceGateway(
        GetBundleArtifactCacheUsageHandler getBundleArtifactCacheUsageHandler,
        PruneBundleArtifactCacheHandler pruneBundleArtifactCacheHandler)
    {
        _getBundleArtifactCacheUsageHandler = getBundleArtifactCacheUsageHandler
            ?? throw new ArgumentNullException(nameof(getBundleArtifactCacheUsageHandler));
        _pruneBundleArtifactCacheHandler = pruneBundleArtifactCacheHandler
            ?? throw new ArgumentNullException(nameof(pruneBundleArtifactCacheHandler));
    }

    public Task<Result<BundleArtifactCacheUsageDto>> GetBundleArtifactCacheUsageAsync(
        GetBundleArtifactCacheUsageQuery query,
        CancellationToken cancellationToken = default)
    {
        return _getBundleArtifactCacheUsageHandler.HandleAsync(query, cancellationToken);
    }

    public Task<Result<BundleArtifactPruneResultDto>> PruneBundleArtifactCacheAsync(
        PruneBundleArtifactCacheCommand command,
        CancellationToken cancellationToken = default)
    {
        return _pruneBundleArtifactCacheHandler.HandleAsync(command, cancellationToken);
    }
}
