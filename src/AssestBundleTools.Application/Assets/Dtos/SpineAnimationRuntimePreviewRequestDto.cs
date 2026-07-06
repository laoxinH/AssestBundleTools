namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示请求 Spine runtime adapter 生成中立动画预览所需的输入。
/// </summary>
public sealed record SpineAnimationRuntimePreviewRequestDto(
    string GroupKey,
    SpineAssetGroupPartDto SkeletonAsset,
    string? SkeletonJson,
    SpineAnimationRuntimeDto Runtime,
    IReadOnlyList<SpineAtlasPageDto> AtlasPages,
    IReadOnlyList<string> AtlasTexts,
    byte[]? SkeletonBinaryContent = null,
    bool IncludeSampledFrames = true,
    AssetPreviewTextSourceDto? SkeletonJsonSource = null,
    IReadOnlyList<AssetPreviewTextSourceDto>? AtlasTextSources = null);
