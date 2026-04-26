using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 基于 JSON 文件的工作区目录持久化实现。
/// </summary>
public sealed class JsonWorkspaceCatalogStore : IWorkspaceCatalogStore
{
    private readonly JsonWorkspaceStoragePaths _storagePaths;

    public JsonWorkspaceCatalogStore(string? rootDirectoryPath = null)
    {
        _storagePaths = string.IsNullOrWhiteSpace(rootDirectoryPath)
            ? JsonWorkspaceStoragePaths.CreateDefault()
            : new JsonWorkspaceStoragePaths(rootDirectoryPath);
    }

    public WorkspaceCatalogSnapshotDto Load()
    {
        var snapshot = JsonWorkspaceFileStorageHelper.TryReadJson<WorkspaceCatalogSnapshotDto>(
            _storagePaths.CatalogFilePath,
            JsonWorkspaceSerialization.SerializerOptions);
        return snapshot ?? WorkspaceCatalogSnapshotDto.Empty;
    }

    public void Save(WorkspaceCatalogSnapshotDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
            _storagePaths.CatalogFilePath,
            snapshot,
            JsonWorkspaceSerialization.SerializerOptions);
    }
}
