namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 为某个工作区数据根目录创建缓存层与暂存层存储实例。
/// </summary>
/// <remarks>
/// 该工厂与 <see cref="IWorkspaceMetadataStoreFactory"/> 分开存在，
/// 目的是明确“工作区元数据”和“可失效缓存 / 变更暂存”属于不同职责层次，
/// 避免后续把重解析结果再次塞回元数据文件。
/// </remarks>
public interface IWorkspaceDataStoreFactory
{
    IBundleArtifactStore CreateBundleArtifactStore(string? rootDirectoryPath = null);

    IBundleIndexCacheStore CreateBundleIndexCacheStore(string? rootDirectoryPath = null);

    IPreviewCacheStore CreatePreviewCacheStore(string? rootDirectoryPath = null);

    IWorkspaceChangeStore CreateWorkspaceChangeStore(string? rootDirectoryPath = null);

    string GetDefaultRootDirectoryPath();
}
