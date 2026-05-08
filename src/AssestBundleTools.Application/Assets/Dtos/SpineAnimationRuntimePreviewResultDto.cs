namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示 Spine runtime adapter 尝试生成中立动画预览后的结果。
/// </summary>
public sealed record SpineAnimationRuntimePreviewResultDto(
    bool IsHandled,
    bool IsCancelled,
    SpineAnimationPreviewDto? Preview,
    IReadOnlyList<SpineAssetGroupDiagnosticDto> Diagnostics)
{
    public static SpineAnimationRuntimePreviewResultDto NotHandled { get; } =
        new(false, false, null, Array.Empty<SpineAssetGroupDiagnosticDto>());

    public static SpineAnimationRuntimePreviewResultDto Cancelled { get; } =
        new(false, true, null, Array.Empty<SpineAssetGroupDiagnosticDto>());

    public static SpineAnimationRuntimePreviewResultDto Handled(
        SpineAnimationPreviewDto? preview,
        IReadOnlyList<SpineAssetGroupDiagnosticDto>? diagnostics = null) =>
        new(true, false, preview, diagnostics ?? Array.Empty<SpineAssetGroupDiagnosticDto>());
}
