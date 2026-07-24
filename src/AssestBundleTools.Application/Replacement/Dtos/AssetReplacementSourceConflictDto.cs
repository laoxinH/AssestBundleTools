using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示同一普通资产命中多个有效外部来源的冲突。
/// </summary>
public sealed record AssetReplacementSourceConflictDto(
    AssetListItemDto Target,
    IReadOnlyList<string> SourceFilePaths);
