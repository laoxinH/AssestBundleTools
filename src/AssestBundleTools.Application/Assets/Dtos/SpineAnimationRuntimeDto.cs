namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示 Spine skeleton 版本对应的 runtime 适配信息。
/// </summary>
public sealed record SpineAnimationRuntimeDto(
    string? SpineVersion,
    int? Major,
    int? Minor,
    string? AdapterKey,
    SpineAnimationRuntimeSupportKind SupportKind);
