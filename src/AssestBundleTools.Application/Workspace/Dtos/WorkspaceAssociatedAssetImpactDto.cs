using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示某个资源修改后需要同步刷新的关联资源。
/// </summary>
public sealed record WorkspaceAssociatedAssetImpactDto(
    BundleId BundleId,
    AssetId AssetId,
    string Name,
    AssetType AssetType,
    long PathId = 0,
    string? AssetTypeDisplayName = null);
