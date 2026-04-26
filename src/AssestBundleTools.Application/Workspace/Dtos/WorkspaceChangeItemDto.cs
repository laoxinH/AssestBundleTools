using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区中单个资源的变更摘要项。
/// </summary>
/// <remarks>
/// 当前阶段先收敛为提取记录和工作区替换记录，
/// 这样批处理页与后续保存摘要页都可以复用同一份读模型。
/// </remarks>
public sealed record WorkspaceChangeItemDto(
    BundleId BundleId,
    AssetId AssetId,
    string BundleDisplayName,
    string AssetName,
    AssetType AssetType,
    bool IsModified,
    DateTimeOffset? LastExtractedAtUtc,
    string? WorkspaceReplacementSourceBundleDisplayName,
    string? WorkspaceReplacementSourceAssetName,
    DateTimeOffset? WorkspaceReplacementAppliedAtUtc,
    string? FileReplacementSourceFileName,
    string? FileReplacementSourcePath,
    DateTimeOffset? FileReplacementAppliedAtUtc,
    DateTimeOffset? LastChangedAtUtc);
