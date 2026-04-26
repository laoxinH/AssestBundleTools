using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 基于 JSON 文件的工作区变更暂存存储。
/// </summary>
public sealed class JsonWorkspaceChangeStore : IWorkspaceChangeStore
{
    private readonly JsonWorkspaceStoragePaths _storagePaths;

    public JsonWorkspaceChangeStore(string? rootDirectoryPath = null)
    {
        _storagePaths = string.IsNullOrWhiteSpace(rootDirectoryPath)
            ? JsonWorkspaceStoragePaths.CreateDefault()
            : new JsonWorkspaceStoragePaths(rootDirectoryPath);
    }

    public WorkspaceChangeSetDto? Load(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return null;
        }

        return JsonWorkspaceFileStorageHelper.TryReadJson<WorkspaceChangeSetDto>(
            _storagePaths.GetWorkspaceChangeFilePath(workspaceKey),
            JsonWorkspaceSerialization.SerializerOptions);
    }

    public void Save(WorkspaceChangeSetDto changeSet)
    {
        ArgumentNullException.ThrowIfNull(changeSet);

        JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
            _storagePaths.GetWorkspaceChangeFilePath(changeSet.WorkspaceKey),
            changeSet,
            JsonWorkspaceSerialization.SerializerOptions);
    }

    public void Delete(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return;
        }

        JsonWorkspaceFileStorageHelper.DeleteFileIfExists(
            _storagePaths.GetWorkspaceChangeFilePath(workspaceKey));
    }
}
