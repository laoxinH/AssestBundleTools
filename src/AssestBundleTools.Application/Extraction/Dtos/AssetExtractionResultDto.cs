using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Extraction.Dtos;

/// <summary>
/// 表示单个资源提取命令的结果摘要。
/// 当前阶段只返回建议输出信息和时间戳，不直接写入真实文件。
/// </summary>
public sealed record AssetExtractionResultDto(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    AssetId AssetId,
    string AssetName,
    string SuggestedOutputPath,
    DateTimeOffset ExtractedAtUtc);
