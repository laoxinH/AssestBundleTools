using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Extraction.Dtos;

/// <summary>
/// 表示 Spine 聚合资源组整组提取后的结果摘要。
/// </summary>
public sealed record SpineAssetGroupExtractionResultDto(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    string GroupId,
    string GroupDisplayName,
    string SuggestedOutputDirectory,
    int ExtractedAssetCount,
    int FailedAssetCount,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<AssetExtractionResultDto> AssetResults,
    IReadOnlyList<SpineAssetGroupPartDto> PlannedParts);
