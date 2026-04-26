using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 更新工作区目录项的固定状态，并重新应用目录排序规则。
/// </summary>
public sealed class SetWorkspaceCatalogEntryPinnedStateHandler
    : ICommandHandler<SetWorkspaceCatalogEntryPinnedStateCommand, Result<WorkspaceCatalogSnapshotDto>>
{
    private readonly IWorkspaceCatalogStore _workspaceCatalogStore;

    public SetWorkspaceCatalogEntryPinnedStateHandler(IWorkspaceCatalogStore workspaceCatalogStore)
    {
        _workspaceCatalogStore = workspaceCatalogStore ?? throw new ArgumentNullException(nameof(workspaceCatalogStore));
    }

    public Task<Result<WorkspaceCatalogSnapshotDto>> HandleAsync(
        SetWorkspaceCatalogEntryPinnedStateCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<WorkspaceCatalogSnapshotDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceKey))
        {
            return Task.FromResult(Result<WorkspaceCatalogSnapshotDto>.Failure(ApplicationErrors.WorkspaceCatalogKeyRequired()));
        }

        var snapshot = _workspaceCatalogStore.Load();
        var updatedEntries = snapshot.RecentWorkspaces
            .Select(entry => string.Equals(entry.WorkspaceKey, command.WorkspaceKey, StringComparison.Ordinal)
                ? entry with { IsPinned = command.IsPinned }
                : entry)
            .OrderByDescending(static entry => entry.IsPinned)
            .ThenByDescending(static entry => entry.LastActivatedAtUtc)
            .ToArray();

        var updatedSnapshot = new WorkspaceCatalogSnapshotDto(
            Version: snapshot.Version <= 0 ? 1 : snapshot.Version,
            LastActiveWorkspaceKey: snapshot.LastActiveWorkspaceKey,
            RecentWorkspaces: updatedEntries);

        _workspaceCatalogStore.Save(updatedSnapshot);
        return Task.FromResult(Result<WorkspaceCatalogSnapshotDto>.Success(updatedSnapshot));
    }
}
