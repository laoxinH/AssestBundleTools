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
    private readonly GetWorkspaceCacheUsageHandler _getWorkspaceCacheUsageHandler;
    private readonly ClearWorkspaceCacheHandler _clearWorkspaceCacheHandler;
    private readonly GetBundleArtifactCacheUsageHandler _getBundleArtifactCacheUsageHandler;
    private readonly PruneBundleArtifactCacheHandler _pruneBundleArtifactCacheHandler;

    public WorkspaceCacheMaintenanceGateway(
        GetWorkspaceCacheUsageHandler getWorkspaceCacheUsageHandler,
        ClearWorkspaceCacheHandler clearWorkspaceCacheHandler,
        GetBundleArtifactCacheUsageHandler getBundleArtifactCacheUsageHandler,
        PruneBundleArtifactCacheHandler pruneBundleArtifactCacheHandler)
    {
        _getWorkspaceCacheUsageHandler = getWorkspaceCacheUsageHandler
            ?? throw new ArgumentNullException(nameof(getWorkspaceCacheUsageHandler));
        _clearWorkspaceCacheHandler = clearWorkspaceCacheHandler
            ?? throw new ArgumentNullException(nameof(clearWorkspaceCacheHandler));
        _getBundleArtifactCacheUsageHandler = getBundleArtifactCacheUsageHandler
            ?? throw new ArgumentNullException(nameof(getBundleArtifactCacheUsageHandler));
        _pruneBundleArtifactCacheHandler = pruneBundleArtifactCacheHandler
            ?? throw new ArgumentNullException(nameof(pruneBundleArtifactCacheHandler));
    }

    public event EventHandler<WorkspaceCacheChangedEventArgs>? WorkspaceCacheChanged;

    public Task<Result<WorkspaceCacheUsageDto>> GetWorkspaceCacheUsageAsync(
        GetWorkspaceCacheUsageQuery query,
        CancellationToken cancellationToken = default)
    {
        return _getWorkspaceCacheUsageHandler.HandleAsync(query, cancellationToken);
    }

    public async Task<Result<WorkspaceCacheClearResultDto>> ClearWorkspaceCacheAsync(
        ClearWorkspaceCacheCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _clearWorkspaceCacheHandler.HandleAsync(command, cancellationToken);
        if (result.IsSuccess)
        {
            WorkspaceCacheChanged?.Invoke(this, new WorkspaceCacheChangedEventArgs(command.WorkspaceKey));
        }

        return result;
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
        return PruneBundleArtifactCacheCoreAsync(command, cancellationToken);
    }

    private async Task<Result<BundleArtifactPruneResultDto>> PruneBundleArtifactCacheCoreAsync(
        PruneBundleArtifactCacheCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _pruneBundleArtifactCacheHandler.HandleAsync(command, cancellationToken);
        if (result.IsSuccess && !string.IsNullOrWhiteSpace(command.WorkspaceKey))
        {
            WorkspaceCacheChanged?.Invoke(this, new WorkspaceCacheChangedEventArgs(command.WorkspaceKey));
        }

        return result;
    }
}
