using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Application.Extraction.Dtos;

namespace AssestBundleTools.Application.Extraction.Commands;

/// <summary>
/// 请求把一个 Spine 聚合资源组的 skeleton / atlas / texture page 整组提取到目标目录。
/// </summary>
public sealed record ExtractSpineAssetGroupCommand(
    WorkspaceId WorkspaceId,
    string WorkspaceKey,
    string WorkspaceDataRootPath,
    BundleId BundleId,
    string GroupId,
    string OutputDirectory,
    bool UseContentAwareSkeletonFileExtension = false);
