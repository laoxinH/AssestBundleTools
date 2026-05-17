using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 从目录索引中移除工作区条目，并清理对应的工作区元数据文件。
/// </summary>
/// <remarks>
/// 当前只删除工具生成的单工作区元数据文件，
/// 不会删除用户选择的数据根目录本身，避免误删其他内容。
/// </remarks>
public sealed class RemoveWorkspaceCatalogEntryHandler
    : ICommandHandler<RemoveWorkspaceCatalogEntryCommand, Result<WorkspaceCatalogSnapshotDto>>
{
    private readonly IWorkspaceCatalogStore _workspaceCatalogStore;
    private readonly IWorkspaceMetadataStoreFactory _workspaceMetadataStoreFactory;
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;

    public RemoveWorkspaceCatalogEntryHandler(
        IWorkspaceCatalogStore workspaceCatalogStore,
        IWorkspaceMetadataStoreFactory workspaceMetadataStoreFactory,
        IWorkspaceDataStoreFactory workspaceDataStoreFactory)
    {
        _workspaceCatalogStore = workspaceCatalogStore ?? throw new ArgumentNullException(nameof(workspaceCatalogStore));
        _workspaceMetadataStoreFactory = workspaceMetadataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceMetadataStoreFactory));
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
    }

    public Task<Result<WorkspaceCatalogSnapshotDto>> HandleAsync(
        RemoveWorkspaceCatalogEntryCommand command,
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
        var targetEntry = snapshot.RecentWorkspaces.FirstOrDefault(entry =>
            string.Equals(entry.WorkspaceKey, command.WorkspaceKey, StringComparison.Ordinal));
        if (targetEntry is null)
        {
            return Task.FromResult(Result<WorkspaceCatalogSnapshotDto>.Success(snapshot));
        }

        var updatedEntries = snapshot.RecentWorkspaces
            .Where(entry => !string.Equals(entry.WorkspaceKey, command.WorkspaceKey, StringComparison.Ordinal))
            .ToArray();

        var updatedSnapshot = new WorkspaceCatalogSnapshotDto(
            Version: snapshot.Version <= 0 ? 1 : snapshot.Version,
            LastActiveWorkspaceKey: string.Equals(snapshot.LastActiveWorkspaceKey, command.WorkspaceKey, StringComparison.Ordinal)
                ? updatedEntries.FirstOrDefault()?.WorkspaceKey
                : snapshot.LastActiveWorkspaceKey,
            RecentWorkspaces: updatedEntries);

        _workspaceCatalogStore.Save(updatedSnapshot);

        DeleteWorkspaceGeneratedData(targetEntry.WorkspaceDataRootPath, targetEntry.WorkspaceKey);

        return Task.FromResult(Result<WorkspaceCatalogSnapshotDto>.Success(updatedSnapshot));
    }

    /// <summary>
    /// 删除工作区记录时，同时清理工具自己生成的元数据、缓存和暂存数据。
    /// 这里不会删除用户选择的数据根目录本身，只删除明确归属于该工作区键的文件。
    /// </summary>
    private void DeleteWorkspaceGeneratedData(string? workspaceDataRootPath, string workspaceKey)
    {
        var metadataStore = _workspaceMetadataStoreFactory.Create(workspaceDataRootPath);
        metadataStore.Delete(workspaceKey);

        var bundleArtifactStore = _workspaceDataStoreFactory.CreateBundleArtifactStore(workspaceDataRootPath);
        bundleArtifactStore.DeleteWorkspace(workspaceKey);

        var bundleIndexCacheStore = _workspaceDataStoreFactory.CreateBundleIndexCacheStore(workspaceDataRootPath);
        bundleIndexCacheStore.DeleteWorkspace(workspaceKey);

        var previewCacheStore = _workspaceDataStoreFactory.CreatePreviewCacheStore(workspaceDataRootPath);
        previewCacheStore.DeleteWorkspace(workspaceKey);

        var workspaceChangeStore = _workspaceDataStoreFactory.CreateWorkspaceChangeStore(workspaceDataRootPath);
        workspaceChangeStore.Delete(workspaceKey);
    }
}
