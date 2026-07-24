namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示普通资产多来源拖入的完整匹配计划。
/// </summary>
public sealed record AssetReplacementSourceMatchPlanDto(
    IReadOnlyList<AssetReplacementSourceMatchDto> Matches,
    IReadOnlyList<AssetReplacementSourceConflictDto> Conflicts)
{
    public bool HasConflicts => Conflicts.Count > 0;
}
