using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 请求获取某个资源的预览内容。
/// </summary>
public sealed record GetAssetPreviewQuery(
    WorkspaceId WorkspaceId,
    string WorkspaceKey,
    string WorkspaceDataRootPath,
    BundleId BundleId,
    AssetId AssetId,
    AssetPreviewSourceOverrideDto? PreviewSourceOverride = null,
    AssetPreviewContentScope ContentScope = AssetPreviewContentScope.Effective);
