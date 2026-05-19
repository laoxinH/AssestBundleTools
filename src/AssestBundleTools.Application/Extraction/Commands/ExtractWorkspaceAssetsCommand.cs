using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Extraction.Commands;

/// <summary>
/// 请求按当前批量提取筛选条件，对工作区资源执行提取。
/// </summary>
public sealed record ExtractWorkspaceAssetsCommand(
    WorkspaceId WorkspaceId,
    AssetType? AssetTypeFilter = null,
    string? OutputDirectory = null,
    string? WorkspaceKey = null,
    string? WorkspaceDataRootPath = null,
    IReadOnlyList<BundleId>? SelectedBundleIds = null,
    IReadOnlyList<WorkspaceBatchExtractionCategoryDto>? SelectedCategories = null,
    IProgress<WorkspaceBatchExtractionProgressDto>? Progress = null);
