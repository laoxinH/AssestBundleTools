using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示外部文件替换命令已被确认并写入工作区变更队列。
/// </summary>
public sealed record AssetFileReplacementResultDto(
    WorkspaceId WorkspaceId,
    BundleId TargetBundleId,
    AssetId TargetAssetId,
    string TargetAssetName,
    string SourceFilePath,
    string SourceFileName,
    AssetType SourceAssetType,
    DateTimeOffset ReplacedAtUtc);
