using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Services;

/// <summary>
/// 用于未配置持久化目录缓存时的空实现。
/// </summary>
public sealed class NoOpSpineGroupCatalogCacheStore : ISpineGroupCatalogCacheStore
{
    public static NoOpSpineGroupCatalogCacheStore Instance { get; } = new();

    private NoOpSpineGroupCatalogCacheStore()
    {
    }

    public SpineGroupCatalogCacheEntryDto? Load(string workspaceKey) => null;

    public void Save(SpineGroupCatalogCacheEntryDto cacheEntry)
    {
    }

    public void DeleteWorkspace(string workspaceKey)
    {
    }
}
