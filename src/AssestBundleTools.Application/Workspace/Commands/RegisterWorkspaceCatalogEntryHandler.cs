using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 负责把工作区登记到应用级目录索引中。
/// </summary>
public sealed class RegisterWorkspaceCatalogEntryHandler
    : ICommandHandler<RegisterWorkspaceCatalogEntryCommand, Result<WorkspaceCatalogEntryDto>>
{
    private readonly IWorkspaceCatalogStore _workspaceCatalogStore;

    public RegisterWorkspaceCatalogEntryHandler(IWorkspaceCatalogStore workspaceCatalogStore)
    {
        _workspaceCatalogStore = workspaceCatalogStore ?? throw new ArgumentNullException(nameof(workspaceCatalogStore));
    }

    public Task<Result<WorkspaceCatalogEntryDto>> HandleAsync(
        RegisterWorkspaceCatalogEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<WorkspaceCatalogEntryDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(command.SourcePath))
        {
            return Task.FromResult(Result<WorkspaceCatalogEntryDto>.Failure(ApplicationErrors.WorkspaceCatalogSourcePathRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceDataRootPath))
        {
            return Task.FromResult(Result<WorkspaceCatalogEntryDto>.Failure(ApplicationErrors.WorkspaceCatalogDataRootPathRequired()));
        }

        var workspaceKey = WorkspaceCatalogKeyFactory.CreateWorkspaceKey(command.SourcePath);
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Task.FromResult(Result<WorkspaceCatalogEntryDto>.Failure(ApplicationErrors.WorkspaceCatalogSourcePathRequired()));
        }

        var snapshot = _workspaceCatalogStore.Load();
        var now = DateTimeOffset.UtcNow;
        var existingEntry = snapshot.RecentWorkspaces.FirstOrDefault(entry =>
            string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.Ordinal));

        var entry = new WorkspaceCatalogEntryDto(
            WorkspaceKey: workspaceKey,
            DisplayName: NormalizeDisplayName(command.DisplayName, command.SourcePath),
            SourcePath: command.SourcePath.Trim(),
            WorkspaceDataRootPath: command.WorkspaceDataRootPath.Trim(),
            WorkspaceMode: command.WorkspaceMode,
            LastActivatedAtUtc: now,
            IsPinned: existingEntry?.IsPinned ?? command.IsPinned,
            LastKnownState: command.LastKnownState ?? existingEntry?.LastKnownState);

        var updatedEntries = snapshot.RecentWorkspaces
            .Where(existing => !string.Equals(existing.WorkspaceKey, workspaceKey, StringComparison.Ordinal))
            .Append(entry)
            .OrderByDescending(static item => item.IsPinned)
            .ThenByDescending(static item => item.LastActivatedAtUtc)
            .ToArray();

        _workspaceCatalogStore.Save(new WorkspaceCatalogSnapshotDto(
            Version: snapshot.Version <= 0 ? 1 : snapshot.Version,
            LastActiveWorkspaceKey: workspaceKey,
            RecentWorkspaces: updatedEntries));

        return Task.FromResult(Result<WorkspaceCatalogEntryDto>.Success(entry));
    }

    private static string NormalizeDisplayName(string? displayName, string sourcePath)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        var trimmedPath = sourcePath.Trim().TrimEnd('\\', '/');
        var lastSlashIndex = trimmedPath.LastIndexOfAny(['\\', '/']);

        return lastSlashIndex >= 0
            ? trimmedPath[(lastSlashIndex + 1)..]
            : trimmedPath;
    }
}
