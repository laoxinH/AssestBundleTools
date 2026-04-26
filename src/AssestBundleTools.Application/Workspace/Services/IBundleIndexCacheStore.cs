using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 负责读写单工作区下每个资源包的索引缓存。
/// </summary>
public interface IBundleIndexCacheStore
{
    BundleIndexCacheEntryDto? Load(string workspaceKey, string bundleCacheKey);

    IReadOnlyList<BundleIndexCacheEntryDto> LoadAll(string workspaceKey);

    void Save(BundleIndexCacheEntryDto cacheEntry);

    void Delete(string workspaceKey, string bundleCacheKey);

    void DeleteWorkspace(string workspaceKey);
}
