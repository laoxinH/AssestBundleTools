using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Services;

/// <summary>
/// 定义 Spine runtime 适配器生成中立绘制帧的应用层端口。
/// </summary>
public interface ISpineAnimationRuntimePreviewService
{
    Task<SpineAnimationRuntimePreviewResultDto> TryBuildPreviewAsync(
        SpineAnimationRuntimePreviewRequestDto request,
        CancellationToken cancellationToken = default);
}
