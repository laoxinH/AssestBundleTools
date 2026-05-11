using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Extraction.Dtos;

/// <summary>
/// 表示单个资源提取命令的结果摘要。
/// </summary>
public sealed record AssetExtractionResultDto(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    AssetId AssetId,
    string AssetName,
    string SuggestedOutputPath,
    DateTimeOffset ExtractedAtUtc,
    string? OutputPath = null,
    string? OutputDirectory = null,
    bool OutputFileExists = false,
    long? OutputSizeInBytes = null,
    AssetPreviewKind? OutputKind = null);
