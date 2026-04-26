namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示一次资源包读取请求。
/// </summary>
public sealed record AssetBundleReadRequestDto(
    string BundlePath,
    string LogicalPath,
    string WorkspaceKey,
    string WorkspaceDataRootPath);
