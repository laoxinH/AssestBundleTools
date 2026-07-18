using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

public sealed class JsonWorkspaceImportRecognitionCacheStore : IWorkspaceImportRecognitionCacheStore
{
    private readonly JsonWorkspaceStoragePaths _storagePaths;

    public JsonWorkspaceImportRecognitionCacheStore(string? rootDirectoryPath = null)
    {
        _storagePaths = string.IsNullOrWhiteSpace(rootDirectoryPath)
            ? JsonWorkspaceStoragePaths.CreateDefault()
            : new JsonWorkspaceStoragePaths(rootDirectoryPath);
    }

    public IReadOnlyList<WorkspaceImportRecognitionCacheEntryDto> LoadAll(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return [];
        }

        var directoryPath = _storagePaths.GetImportRecognitionCacheDirectoryPath(workspaceKey);
        if (!Directory.Exists(directoryPath))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly)
            .Select(filePath => JsonWorkspaceFileStorageHelper.TryReadJson<WorkspaceImportRecognitionCacheEntryDto>(
                filePath,
                JsonWorkspaceSerialization.SerializerOptions))
            .Where(static entry => entry is not null)
            .Cast<WorkspaceImportRecognitionCacheEntryDto>()
            .ToArray();
    }

    public void Save(WorkspaceImportRecognitionCacheEntryDto cacheEntry)
    {
        ArgumentNullException.ThrowIfNull(cacheEntry);
        JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
            _storagePaths.GetImportRecognitionCacheFilePath(cacheEntry.WorkspaceKey, cacheEntry.BundleCacheKey),
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
            _storagePaths.GetImportRecognitionCacheFilePath(workspaceKey, bundleCacheKey));
    }

    public void DeleteWorkspace(string workspaceKey)
    {
        if (!string.IsNullOrWhiteSpace(workspaceKey))
        {
            JsonWorkspaceFileStorageHelper.DeleteDirectoryIfExists(
                _storagePaths.GetImportRecognitionCacheDirectoryPath(workspaceKey));
        }
    }
}
