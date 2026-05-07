using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示当前 Bundle 视图下可识别的 Spine 资源组列表。
/// </summary>
public sealed record SpineAssetGroupListResultDto(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    string BundleDisplayName,
    string? SearchKeyword,
    int TotalCount,
    int FilteredCount,
    IReadOnlyList<SpineAssetGroupDto> Items);
