using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 基于 JSON 文件的资源包索引缓存存储。
/// </summary>
public sealed class JsonBundleIndexCacheStore : IBundleIndexCacheStore
{
    private readonly JsonWorkspaceStoragePaths _storagePaths;

    public JsonBundleIndexCacheStore(string? rootDirectoryPath = null)
    {
        _storagePaths = string.IsNullOrWhiteSpace(rootDirectoryPath)
            ? JsonWorkspaceStoragePaths.CreateDefault()
            : new JsonWorkspaceStoragePaths(rootDirectoryPath);
    }

    public BundleIndexCacheEntryDto? Load(string workspaceKey, string bundleCacheKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(bundleCacheKey))
        {
            return null;
        }

        var cacheFilePath = _storagePaths.GetBundleIndexCacheFilePath(workspaceKey, bundleCacheKey);
        return JsonWorkspaceFileStorageHelper.TryReadJson<BundleIndexCacheEntryDto>(
            cacheFilePath,
            JsonWorkspaceSerialization.SerializerOptions);
    }

    public IReadOnlyList<BundleIndexCacheEntryDto> LoadAll(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Array.Empty<BundleIndexCacheEntryDto>();
        }

        var workspaceCacheDirectoryPath = _storagePaths.GetBundleIndexCacheDirectoryPath(workspaceKey);
        if (!Directory.Exists(workspaceCacheDirectoryPath))
        {
            return Array.Empty<BundleIndexCacheEntryDto>();
        }

        var cacheEntries = new List<BundleIndexCacheEntryDto>();

        foreach (var cacheFilePath in Directory.EnumerateFiles(workspaceCacheDirectoryPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            var cacheEntry = JsonWorkspaceFileStorageHelper.TryReadJson<BundleIndexCacheEntryDto>(
                cacheFilePath,
                JsonWorkspaceSerialization.SerializerOptions);
            if (cacheEntry is not null)
            {
                cacheEntries.Add(cacheEntry);
            }
        }

        return cacheEntries;
    }

    public void Save(BundleIndexCacheEntryDto cacheEntry)
    {
        ArgumentNullException.ThrowIfNull(cacheEntry);

        var cacheFilePath = _storagePaths.GetBundleIndexCacheFilePath(cacheEntry.WorkspaceKey, cacheEntry.BundleCacheKey);
        JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
            cacheFilePath,
            cacheEntry,
            JsonWorkspaceSerialization.SerializerOptions);
    }

    public void Delete(string workspaceKey, string bundleCacheKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(bundleCacheKey))
        {
            return;
        }

        JsonWorkspaceFileStorageHelper.DeleteFileIfExists(
            _storagePaths.GetBundleIndexCacheFilePath(workspaceKey, bundleCacheKey));
    }

    public void DeleteWorkspace(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return;
        }

        JsonWorkspaceFileStorageHelper.DeleteDirectoryIfExists(
            _storagePaths.GetBundleIndexCacheDirectoryPath(workspaceKey));
    }
}
