namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示 Spine 动画预览的 runtime 适配状态。
/// </summary>
public enum SpineAnimationRuntimeSupportKind
{
    OfficialAdapterAvailable = 0,
    VersionMissing = 1,
    UnsupportedVersion = 2
}
