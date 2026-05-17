using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示一次真实资源预览生成请求。
/// </summary>
public sealed record AssetPreviewBuildRequestDto(
    string ResolvedBundlePath,
    string WorkspaceKey,
    string WorkspaceDataRootPath,
    string AssetId,
    string AssetName,
    string AssetPath,
    string? ContainerPath,
    AssetType AssetType,
    string AssetTypeDisplayName,
    long SizeInBytes,
    string SerializedFileName,
    long PathId,
    string? BundleSessionCacheKey = null);
