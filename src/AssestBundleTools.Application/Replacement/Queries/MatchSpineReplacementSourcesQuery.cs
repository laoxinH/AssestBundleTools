using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Queries;

/// <summary>
/// 请求把目录或多个外部文件匹配到一个 Spine 资源组的组成资产。
/// </summary>
public sealed record MatchSpineReplacementSourcesQuery(
    WorkspaceId WorkspaceId,
    string WorkspaceKey,
    string WorkspaceDataRootPath,
    SpineAssetGroupDto Group,
    IReadOnlyList<string> SourcePaths);
