using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Queries;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 聚合工作区目录相关 Handler，避免 Presentation 直接感知具体用例实现。
/// </summary>
public sealed class WorkspaceCatalogGateway : IWorkspaceCatalogGateway
{
    private readonly GetWorkspaceCatalogSnapshotHandler _getWorkspaceCatalogSnapshotHandler;
    private readonly RegisterWorkspaceCatalogEntryHandler _registerWorkspaceCatalogEntryHandler;
    private readonly RemoveWorkspaceCatalogEntryHandler _removeWorkspaceCatalogEntryHandler;
    private readonly SetWorkspaceCatalogEntryPinnedStateHandler _setWorkspaceCatalogEntryPinnedStateHandler;

    public WorkspaceCatalogGateway(
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
