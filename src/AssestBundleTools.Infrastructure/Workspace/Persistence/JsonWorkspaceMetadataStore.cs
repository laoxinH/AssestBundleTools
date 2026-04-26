using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 基于 JSON 文件的单工作区元数据持久化实现。
/// </summary>
public sealed class JsonWorkspaceMetadataStore : IWorkspaceMetadataStore
{
    private readonly JsonWorkspaceStoragePaths _storagePaths;

    public JsonWorkspaceMetadataStore(string? rootDirectoryPath = null)
    {
        _storagePaths = string.IsNullOrWhiteSpace(rootDirectoryPath)
            ? JsonWorkspaceStoragePaths.CreateDefault()
            : new JsonWorkspaceStoragePaths(rootDirectoryPath);
    }

    public WorkspaceMetadataDto? Load(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return null;
        }

        return JsonWorkspaceFileStorageHelper.TryReadJson<WorkspaceMetadataDto>(
            _storagePaths.GetMetadataFilePath(workspaceKey),
            JsonWorkspaceSerialization.SerializerOptions);
    }

    public void Save(WorkspaceMetadataDto metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
            _storagePaths.GetMetadataFilePath(metadata.WorkspaceKey),
            metadata,
            JsonWorkspaceSerialization.SerializerOptions);
    }

    public void Delete(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return;
        }

        JsonWorkspaceFileStorageHelper.DeleteFileIfExists(_storagePaths.GetMetadataFilePath(workspaceKey));
    }
}
