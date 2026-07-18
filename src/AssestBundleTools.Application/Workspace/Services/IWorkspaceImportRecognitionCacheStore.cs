using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

public interface IWorkspaceImportRecognitionCacheStore
{
    IReadOnlyList<WorkspaceImportRecognitionCacheEntryDto> LoadAll(string workspaceKey);

    void Save(WorkspaceImportRecognitionCacheEntryDto cacheEntry);

    void Delete(string workspaceKey, string bundleCacheKey);

    void DeleteWorkspace(string workspaceKey);
}

internal sealed class NoOpWorkspaceImportRecognitionCacheStore : IWorkspaceImportRecognitionCacheStore
{
    public static NoOpWorkspaceImportRecognitionCacheStore Instance { get; } = new();

    public IReadOnlyList<WorkspaceImportRecognitionCacheEntryDto> LoadAll(string workspaceKey) => [];

    public void Save(WorkspaceImportRecognitionCacheEntryDto cacheEntry)
    {
    }

    public void Delete(string workspaceKey, string bundleCacheKey)
    {
    }

    public void DeleteWorkspace(string workspaceKey)
    {
    }
}
