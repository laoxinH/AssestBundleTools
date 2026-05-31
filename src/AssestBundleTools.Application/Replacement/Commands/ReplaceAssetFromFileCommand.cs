using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Commands;

/// <summary>
/// 请求将外部文件作为替换来源写入当前工作区。
/// </summary>
public sealed record ReplaceAssetFromFileCommand(
    WorkspaceId WorkspaceId,
    BundleId TargetBundleId,
    AssetId TargetAssetId,
    string SourceFilePath,
    AssetType SourceAssetType,
    string? WorkspaceKey = null,
    string? WorkspaceDataRootPath = null,
    ReplacementOperationContextDto? OperationContext = null);
