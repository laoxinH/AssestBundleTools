using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Queries;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 将工作区目录管理的应用层处理器聚合为 Presentation 可直接使用的门面。
/// </summary>
public sealed class WorkspaceCatalogService : IWorkspaceCatalogService
{
    private readonly GetWorkspaceCatalogSnapshotHandler _getWorkspaceCatalogSnapshotHandler;
    private readonly RegisterWorkspaceCatalogEntryHandler _registerWorkspaceCatalogEntryHandler;
    private readonly RemoveWorkspaceCatalogEntryHandler _removeWorkspaceCatalogEntryHandler;
    private readonly SetWorkspaceCatalogEntryPinnedStateHandler _setWorkspaceCatalogEntryPinnedStateHandler;

    public WorkspaceCatalogService(
        GetWorkspaceCatalogSnapshotHandler getWorkspaceCatalogSnapshotHandler,
        RegisterWorkspaceCatalogEntryHandler registerWorkspaceCatalogEntryHandler,
        RemoveWorkspaceCatalogEntryHandler removeWorkspaceCatalogEntryHandler,
        SetWorkspaceCatalogEntryPinnedStateHandler setWorkspaceCatalogEntryPinnedStateHandler)
    {
        _getWorkspaceCatalogSnapshotHandler = getWorkspaceCatalogSnapshotHandler
            ?? throw new ArgumentNullException(nameof(getWorkspaceCatalogSnapshotHandler));
        _registerWorkspaceCatalogEntryHandler = registerWorkspaceCatalogEntryHandler
            ?? throw new ArgumentNullException(nameof(registerWorkspaceCatalogEntryHandler));
        _removeWorkspaceCatalogEntryHandler = removeWorkspaceCatalogEntryHandler
            ?? throw new ArgumentNullException(nameof(removeWorkspaceCatalogEntryHandler));
        _setWorkspaceCatalogEntryPinnedStateHandler = setWorkspaceCatalogEntryPinnedStateHandler
            ?? throw new ArgumentNullException(nameof(setWorkspaceCatalogEntryPinnedStateHandler));
    }

    public Task<Result<WorkspaceCatalogSnapshotDto>> GetCatalogSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return _getWorkspaceCatalogSnapshotHandler.HandleAsync(new GetWorkspaceCatalogSnapshotQuery(), cancellationToken);
    }

    public Task<Result<WorkspaceCatalogEntryDto>> RegisterWorkspaceEntryAsync(
        RegisterWorkspaceCatalogEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        return _registerWorkspaceCatalogEntryHandler.HandleAsync(command, cancellationToken);
    }

    public Task<Result<WorkspaceCatalogSnapshotDto>> RemoveWorkspaceEntryAsync(
        RemoveWorkspaceCatalogEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        return _removeWorkspaceCatalogEntryHandler.HandleAsync(command, cancellationToken);
    }

    public Task<Result<WorkspaceCatalogSnapshotDto>> SetWorkspaceEntryPinnedStateAsync(
        SetWorkspaceCatalogEntryPinnedStateCommand command,
        CancellationToken cancellationToken = default)
    {
        return _setWorkspaceCatalogEntryPinnedStateHandler.HandleAsync(command, cancellationToken);
    }
}
