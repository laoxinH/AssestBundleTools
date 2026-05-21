using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Commands;

/// <summary>
/// 请求重做当前资源最近一次已撤销的替换。
/// </summary>
public sealed record RedoAssetReplacementCommand(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    AssetId AssetId,
    string? WorkspaceKey = null,
    string? WorkspaceDataRootPath = null,
    string? ReplacementOperationId = null);
