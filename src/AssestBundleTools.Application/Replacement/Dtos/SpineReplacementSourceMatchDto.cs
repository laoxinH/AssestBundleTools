using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示一个按名称、角色和真实 skeleton 内容格式确认的 Spine 替换来源。
/// </summary>
public sealed record SpineReplacementSourceMatchDto(
    AssetId TargetAssetId,
    string SourceFilePath);
