namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 用于按给定数据根目录创建工作区元数据存储实例。
/// </summary>
/// <remarks>
/// 目录索引仍保持全局统一，
/// 但单工作区元数据允许落到用户指定的数据目录中。
/// </remarks>
public interface IWorkspaceMetadataStoreFactory
{
    IWorkspaceMetadataStore Create(string? rootDirectoryPath = null);

    string GetDefaultRootDirectoryPath();
}
