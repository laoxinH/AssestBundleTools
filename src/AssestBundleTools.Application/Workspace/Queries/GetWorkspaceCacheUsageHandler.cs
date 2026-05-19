using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Queries;

/// <summary>
/// 读取当前工作区可重建缓存的聚合占用摘要。
/// </summary>
public sealed class GetWorkspaceCacheUsageHandler
    : IQueryHandler<GetWorkspaceCacheUsageQuery, Result<WorkspaceCacheUsageDto>>
{
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;

    public GetWorkspaceCacheUsageHandler(IWorkspaceDataStoreFactory workspaceDataStoreFactory)
    {
        _workspaceDataStoreFactory = workspaceDataStoreFactory
                                     ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
    }

    public Task<Result<WorkspaceCacheUsageDto>> HandleAsync(
        GetWorkspaceCacheUsageQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<WorkspaceCacheUsageDto>.Cancelled());
        }

        var bundleArtifactStore = _workspaceDataStoreFactory.CreateBundleArtifactStore(query.WorkspaceDataRootPath);
        var previewCacheStore = _workspaceDataStoreFactory.CreatePreviewCacheStore(query.WorkspaceDataRootPath);
        var bundleArtifactUsage = bundleArtifactStore.GetUsage(query.WorkspaceKey);
        var previewCacheUsage = previewCacheStore.GetUsage(query.WorkspaceKey);
        var usage = new WorkspaceCacheUsageDto(
            TotalSizeInBytes: bundleArtifactUsage.TotalSizeInBytes + previewCacheUsage.TotalSizeInBytes,
            TotalEntryCount: bundleArtifactUsage.ArtifactCount + previewCacheUsage.EntryCount,
            BundleArtifactCache: bundleArtifactUsage,
            PreviewCache: previewCacheUsage);

        return Task.FromResult(Result<WorkspaceCacheUsageDto>.Success(usage));
    }
}
