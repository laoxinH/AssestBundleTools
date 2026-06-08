using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 基于 JSON 文件的 Spine 组目录缓存存储。
/// </summary>
public sealed class JsonSpineGroupCatalogCacheStore : ISpineGroupCatalogCacheStore
{
    private readonly JsonWorkspaceStoragePaths _storagePaths;

    public JsonSpineGroupCatalogCacheStore(string? rootDirectoryPath = null)
    {
        _storagePaths = string.IsNullOrWhiteSpace(rootDirectoryPath)
            ? JsonWorkspaceStoragePaths.CreateDefault()
            : new JsonWorkspaceStoragePaths(rootDirectoryPath);
    }

    public SpineGroupCatalogCacheEntryDto? Load(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return null;
        }

        return JsonWorkspaceFileStorageHelper.TryReadJson<SpineGroupCatalogCacheEntryDto>(
            _storagePaths.GetSpineGroupCatalogCacheFilePath(workspaceKey),
            JsonWorkspaceSerialization.SerializerOptions);
    }

    public void Save(SpineGroupCatalogCacheEntryDto cacheEntry)
    {
        ArgumentNullException.ThrowIfNull(cacheEntry);

        JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
            _storagePaths.GetSpineGroupCatalogCacheFilePath(cacheEntry.WorkspaceKey),
            cacheEntry,
            JsonWorkspaceSerialization.SerializerOptions);
    }

    public void DeleteWorkspace(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return;
        }

        JsonWorkspaceFileStorageHelper.DeleteDirectoryIfExists(
            _storagePaths.GetSpineGroupCatalogCacheDirectoryPath(workspaceKey));
    }
}
