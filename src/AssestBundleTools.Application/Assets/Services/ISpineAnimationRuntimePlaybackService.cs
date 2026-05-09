using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Services;

/// <summary>
/// 定义 Spine runtime 实时播放 session 的应用层端口。
/// </summary>
public interface ISpineAnimationRuntimePlaybackService
{
    Task<SpineAnimationRuntimePlaybackSessionResultDto> TryCreateSessionAsync(
        SpineAnimationRuntimePlaybackSourceDto source,
        CancellationToken cancellationToken = default);

    Task<SpineAnimationRuntimeFixedFrameSamplingResultDto> SampleFixedFramesAsync(
        SpineAnimationRuntimeFixedFrameSamplingRequestDto request,
        CancellationToken cancellationToken = default);
}

public interface ISpineAnimationRuntimePlaybackSession : IDisposable
{
    Task<SpineAnimationRuntimePlaybackFrameResultDto> GetFrameAsync(
        SpineAnimationRuntimePlaybackFrameRequestDto request,
        CancellationToken cancellationToken = default);
}
