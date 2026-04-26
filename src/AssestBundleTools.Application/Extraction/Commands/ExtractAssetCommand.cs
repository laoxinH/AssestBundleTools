using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Extraction.Commands;

/// <summary>
/// 请求提取单个资源。
/// 当前阶段先打通命令流和工作区状态记录，不直接落真实文件写出。
/// </summary>
public sealed record ExtractAssetCommand(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    AssetId AssetId);
