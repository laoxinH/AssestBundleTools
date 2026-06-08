using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Services;

/// <summary>
/// 读写工作区 Spine 组目录派生缓存。
/// </summary>
public interface ISpineGroupCatalogCacheStore
{
    SpineGroupCatalogCacheEntryDto? Load(string workspaceKey);

    void Save(SpineGroupCatalogCacheEntryDto cacheEntry);

    void DeleteWorkspace(string workspaceKey);
}
