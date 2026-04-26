using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Services;

/// <summary>
/// 负责把工作区中的替换变更项整理成批量替换计划。
/// </summary>
/// <remarks>
/// 这里不直接执行单资源替换命令，而是复用工作区变更摘要，
/// 把已进入替换队列的项聚合为批量任务可消费的内部模型。
/// </remarks>
internal static class WorkspaceBatchReplacementPlanner
{
    public static WorkspaceBatchReplacementPlan BuildPlan(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceId workspaceId,
        BatchReplacementSourceKind sourceFilter)
    {
        ArgumentNullException.ThrowIfNull(workspaceSessionRegistry);

        if (!workspaceSessionRegistry.TryGetWorkspaceChangeSummary(workspaceId, out var changeSummary) ||
            changeSummary is null)
        {
            return new WorkspaceBatchReplacementPlan(
                WorkspaceId: workspaceId,
                WorkspaceDisplayName: string.Empty,
                SourceFilter: sourceFilter,
                Items: Array.Empty<PlannedWorkspaceBatchReplacement>());
        }

        var items = changeSummary.Items
            .Where(item => MatchesSourceFilter(item, sourceFilter))
            .Select(item => new PlannedWorkspaceBatchReplacement(
                BundleId: item.BundleId,
                AssetId: item.AssetId,
                BundleDisplayName: item.BundleDisplayName,
                AssetName: item.AssetName,
                HasWorkspaceReplacement: !string.IsNullOrWhiteSpace(item.WorkspaceReplacementSourceAssetName),
                HasFileReplacement: !string.IsNullOrWhiteSpace(item.FileReplacementSourceFileName)))
            .ToArray();

        return new WorkspaceBatchReplacementPlan(
            WorkspaceId: changeSummary.WorkspaceId,
            WorkspaceDisplayName: changeSummary.WorkspaceDisplayName,
            SourceFilter: sourceFilter,
            Items: items);
    }

    private static bool MatchesSourceFilter(
        Application.Workspace.Dtos.WorkspaceChangeItemDto item,
        BatchReplacementSourceKind sourceFilter)
    {
        var hasWorkspaceReplacement = !string.IsNullOrWhiteSpace(item.WorkspaceReplacementSourceAssetName);
        var hasFileReplacement = !string.IsNullOrWhiteSpace(item.FileReplacementSourceFileName);

        return sourceFilter switch
        {
            BatchReplacementSourceKind.Workspace => hasWorkspaceReplacement,
            BatchReplacementSourceKind.File => hasFileReplacement,
            _ => hasWorkspaceReplacement || hasFileReplacement
        };
    }
}

/// <summary>
/// 承载批量替换计划的内部模型。
/// </summary>
internal sealed record WorkspaceBatchReplacementPlan(
    WorkspaceId WorkspaceId,
    string WorkspaceDisplayName,
    BatchReplacementSourceKind SourceFilter,
    IReadOnlyList<PlannedWorkspaceBatchReplacement> Items);

/// <summary>
/// 表示单个已进入替换队列的资源项。
/// </summary>
internal sealed record PlannedWorkspaceBatchReplacement(
    BundleId BundleId,
    Domain.Assets.AssetId AssetId,
    string BundleDisplayName,
    string AssetName,
    bool HasWorkspaceReplacement,
    bool HasFileReplacement);
