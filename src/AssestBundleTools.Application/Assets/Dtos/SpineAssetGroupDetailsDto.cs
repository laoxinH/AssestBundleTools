namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示 Spine 资源组在 atlas 文本解析后的详情读模型。
/// </summary>
public sealed record SpineAssetGroupDetailsDto(
    SpineAssetGroupDto Group,
    IReadOnlyList<SpineSkeletonSummaryDto> SkeletonSummaries,
    IReadOnlyList<SpineAtlasPageDto> AtlasPages,
    IReadOnlyList<SpineAssetGroupDiagnosticDto> Diagnostics,
    SpineAnimationPreviewDto? AnimationPreview = null);
