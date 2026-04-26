using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Queries;

/// <summary>
/// 读取当前应用级工作区目录快照。
/// </summary>
public sealed class GetWorkspaceCatalogSnapshotHandler
    : IQueryHandler<GetWorkspaceCatalogSnapshotQuery, Result<WorkspaceCatalogSnapshotDto>>
{
    private readonly IWorkspaceCatalogStore _workspaceCatalogStore;

    public GetWorkspaceCatalogSnapshotHandler(IWorkspaceCatalogStore workspaceCatalogStore)
    {
        _workspaceCatalogStore = workspaceCatalogStore ?? throw new ArgumentNullException(nameof(workspaceCatalogStore));
    }

    public Task<Result<WorkspaceCatalogSnapshotDto>> HandleAsync(
        GetWorkspaceCatalogSnapshotQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<WorkspaceCatalogSnapshotDto>.Cancelled());
        }

        var snapshot = _workspaceCatalogStore.Load();
        return Task.FromResult(Result<WorkspaceCatalogSnapshotDto>.Success(snapshot));
    }
}
