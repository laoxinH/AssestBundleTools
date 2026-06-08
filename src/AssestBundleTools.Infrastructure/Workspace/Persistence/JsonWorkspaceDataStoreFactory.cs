using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 为工作区数据根目录创建缓存层和暂存层存储实例。
/// </summary>
public sealed class JsonWorkspaceDataStoreFactory : IWorkspaceDataStoreFactory
{
    public IBundleArtifactStore CreateBundleArtifactStore(string? rootDirectoryPath = null)
    {
        return string.IsNullOrWhiteSpace(rootDirectoryPath)
            ? new JsonBundleArtifactStore()
            : new JsonBundleArtifactStore(rootDirectoryPath);
    }

    public IBundleIndexCacheStore CreateBundleIndexCacheStore(string? rootDirectoryPath = null)
    {
        return string.IsNullOrWhiteSpace(rootDirectoryPath)
            ? new JsonBundleIndexCacheStore()
            : new JsonBundleIndexCacheStore(rootDirectoryPath);
    }

    public ISpineGroupCatalogCacheStore CreateSpineGroupCatalogCacheStore(string? rootDirectoryPath = null)
    {
        return string.IsNullOrWhiteSpace(rootDirectoryPath)
            ? new JsonSpineGroupCatalogCacheStore()
            : new JsonSpineGroupCatalogCacheStore(rootDirectoryPath);
    }

    public IPreviewCacheStore CreatePreviewCacheStore(string? rootDirectoryPath = null)
    {
        return string.IsNullOrWhiteSpace(rootDirectoryPath)
            ? new JsonPreviewCacheStore()
            : new JsonPreviewCacheStore(rootDirectoryPath);
    }

    public IWorkspaceChangeStore CreateWorkspaceChangeStore(string? rootDirectoryPath = null)
    {
        return string.IsNullOrWhiteSpace(rootDirectoryPath)
            ? new JsonWorkspaceChangeStore()
            : new JsonWorkspaceChangeStore(rootDirectoryPath);
    }

    public string GetDefaultRootDirectoryPath()
    {
        return JsonWorkspaceStoragePaths.CreateDefault().RootDirectoryPath;
    }
}
