using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Extraction.Commands;

/// <summary>
/// 请求提取单个资源。
/// </summary>
public sealed record ExtractAssetCommand(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    AssetId AssetId,
    string? WorkspaceKey = null,
    string? WorkspaceDataRootPath = null,
    string? SuggestedOutputPath = null,
    bool PreserveSuggestedFileName = false);
