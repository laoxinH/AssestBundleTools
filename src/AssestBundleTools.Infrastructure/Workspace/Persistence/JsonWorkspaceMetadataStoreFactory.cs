using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 为不同工作区数据根目录创建 JSON 元数据存储实例。
/// </summary>
public sealed class JsonWorkspaceMetadataStoreFactory : IWorkspaceMetadataStoreFactory
{
    public IWorkspaceMetadataStore Create(string? rootDirectoryPath = null)
    {
        return string.IsNullOrWhiteSpace(rootDirectoryPath)
            ? new JsonWorkspaceMetadataStore()
            : new JsonWorkspaceMetadataStore(rootDirectoryPath);
    }

    public string GetDefaultRootDirectoryPath()
    {
        return JsonWorkspaceStoragePaths.CreateDefault().RootDirectoryPath;
    }
}
