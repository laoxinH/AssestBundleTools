using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Queries;

/// <summary>
/// 请求把文件和目录混合来源匹配到当前资产列表中的普通资产。
/// </summary>
public sealed record MatchAssetReplacementSourcesQuery(
    WorkspaceId WorkspaceId,
    string WorkspaceKey,
    string WorkspaceDataRootPath,
    IReadOnlyList<AssetListItemDto> Targets,
    IReadOnlyList<SpineAssetGroupDto> SpineGroups,
    IReadOnlyList<string> SourcePaths);
