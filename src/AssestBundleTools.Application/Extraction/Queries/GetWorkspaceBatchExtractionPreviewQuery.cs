using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Extraction.Queries;

/// <summary>
/// 查询当前工作区的批量提取预览摘要。
/// </summary>
public sealed record GetWorkspaceBatchExtractionPreviewQuery(
    WorkspaceId WorkspaceId,
    AssetType? AssetTypeFilter = null,
    string? OutputDirectory = null);
