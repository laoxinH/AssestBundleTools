using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Queries;

/// <summary>
/// 读取标准化资源包 artifact 缓存占用摘要。
/// </summary>
public sealed class GetBundleArtifactCacheUsageHandler
    : IQueryHandler<GetBundleArtifactCacheUsageQuery, Result<BundleArtifactCacheUsageDto>>
{
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;

    public GetBundleArtifactCacheUsageHandler(IWorkspaceDataStoreFactory workspaceDataStoreFactory)
    {
        _workspaceDataStoreFactory = workspaceDataStoreFactory
                                     ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
    }

    public Task<Result<BundleArtifactCacheUsageDto>> HandleAsync(
        GetBundleArtifactCacheUsageQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<BundleArtifactCacheUsageDto>.Cancelled());
        }

        var store = _workspaceDataStoreFactory.CreateBundleArtifactStore(query.WorkspaceDataRootPath);
        var usage = store.GetUsage(query.WorkspaceKey);
        return Task.FromResult(Result<BundleArtifactCacheUsageDto>.Success(usage));
    }
}
