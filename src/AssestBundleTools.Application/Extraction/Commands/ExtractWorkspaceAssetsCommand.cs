using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Extraction.Commands;

/// <summary>
/// 请求按当前批量提取筛选条件，对工作区资源执行提取骨架。
/// </summary>
public sealed record ExtractWorkspaceAssetsCommand(
    WorkspaceId WorkspaceId,
    AssetType? AssetTypeFilter = null,
    string? OutputDirectory = null);
