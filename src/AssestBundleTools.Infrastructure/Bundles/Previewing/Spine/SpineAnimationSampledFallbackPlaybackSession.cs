using System.Diagnostics;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;

internal sealed class SpineAnimationSampledFallbackPlaybackSession : ISpineAnimationRuntimePlaybackSession
{
    private const string AdapterKeyPrefix = "JSONFallback";
    private readonly string _adapterKey;
    private readonly SpineAnimationSampledPlaybackSourceDto _source;
    private bool _isDisposed;

    public SpineAnimationSampledFallbackPlaybackSession(
        string? adapterKey,
        SpineAnimationSampledPlaybackSourceDto source)
    {
        _adapterKey = string.IsNullOrWhiteSpace(adapterKey)
            ? AdapterKeyPrefix
            : string.Concat(AdapterKeyPrefix, ":", adapterKey);
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public Task<SpineAnimationRuntimePlaybackFrameResultDto> GetFrameAsync(
        SpineAnimationRuntimePlaybackFrameRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(SpineAnimationRuntimePlaybackFrameResultDto.Cancelled);
        }

        if (_isDisposed)
        {
            return Task.FromResult(SpineAnimationRuntimePlaybackFrameResultDto.NotHandled);
        }

        var stopwatch = Stopwatch.StartNew();
        var frame = ResolveFrame(request, out var duration);
        stopwatch.Stop();
        if (frame is null)
        {
            return Task.FromResult(SpineAnimationRuntimePlaybackFrameResultDto.NotHandled);
        }

        return Task.FromResult(SpineAnimationRuntimePlaybackFrameResultDto.Handled(
            frame,
            request.PlaybackTime,
            duration,
            CreateDiagnostics(request.ShowSetupPose, stopwatch.Elapsed.TotalMilliseconds, frame)));
    }

    public void Dispose()
    {
        _isDisposed = true;
    }

    private SpineAnimationFrameDto? ResolveFrame(
        SpineAnimationRuntimePlaybackFrameRequestDto request,
        out double duration)
    {
        duration = 0d;
        var skin = ResolveSkinPreview(request.SkinName);
        if (request.ShowSetupPose)
        {
            return skin?.SetupFrame ?? _source.SetupFrame;
        }

        var animations = skin?.Animations is { Count: > 0 }
            ? skin.Animations
            : _source.Animations;
        var animation = ResolveAnimation(animations, request.AnimationName);
        duration = Math.Max(0d, animation?.Duration ?? 0d);
        var frames = animation?.SampledFrames;
        if (frames is not { Count: > 0 })
        {
            return skin?.SetupFrame ?? _source.SetupFrame;
        }

        return frames[ResolveFrameIndex(frames, NormalizePlaybackTime(request.PlaybackTime, duration))];
    }

    private SpineAnimationSkinPreviewDto? ResolveSkinPreview(string? skinName)
    {
        if (!string.IsNullOrWhiteSpace(skinName))
        {
            var requestedSkin = _source.SkinPreviews.FirstOrDefault(skin =>
                string.Equals(skin.Name, skinName, StringComparison.Ordinal));
            if (requestedSkin is not null)
            {
                return requestedSkin;
            }
        }

        return _source.SkinPreviews.FirstOrDefault(static skin =>
                   string.Equals(skin.Name, "default", StringComparison.OrdinalIgnoreCase)) ??
               _source.SkinPreviews.FirstOrDefault();
    }

    private static SpineAnimationClipDto? ResolveAnimation(
        IReadOnlyList<SpineAnimationClipDto> animations,
        string? animationName)
    {
        if (!string.IsNullOrWhiteSpace(animationName))
        {
            var requestedAnimation = animations.FirstOrDefault(animation =>
                string.Equals(animation.Name, animationName, StringComparison.Ordinal));
            if (requestedAnimation is not null)
            {
                return requestedAnimation;
            }
        }

        return animations.FirstOrDefault();
    }

    private static double NormalizePlaybackTime(double playbackTime, double duration)
    {
        if (duration <= 0d)
        {
            return 0d;
        }

        if (playbackTime < 0d)
        {
            return 0d;
        }

        return playbackTime > duration ? playbackTime % duration : playbackTime;
    }

    private static int ResolveFrameIndex(IReadOnlyList<SpineAnimationFrameDto> frames, double time)
    {
        if (frames.Count <= 1)
        {
            return 0;
        }

        var low = 0;
        var high = frames.Count - 1;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            if (frames[mid].Time <= time)
            {
                low = mid + 1;
                continue;
            }

            high = mid - 1;
        }

        return Math.Clamp(high, 0, frames.Count - 1);
    }

    private SpineAnimationRuntimePlaybackFrameDiagnosticsDto CreateDiagnostics(
        bool isSetupPose,
        double totalMilliseconds,
        SpineAnimationFrameDto frame)
    {
        return new SpineAnimationRuntimePlaybackFrameDiagnosticsDto(
            _adapterKey,
            isSetupPose,
            totalMilliseconds,
            SkeletonCreateMilliseconds: 0d,
            SkeletonResetMilliseconds: 0d,
            IsSkeletonReused: true,
            AnimationApplyMilliseconds: 0d,
            WorldTransformMilliseconds: 0d,
            DrawCommandBuildMilliseconds: 0d,
            frame.DrawCommands.Count,
            frame.DrawCommands.Sum(static command => command.MeshVertices.Count),
            frame.DrawCommands.Sum(static command => command.MeshTriangles.Count));
    }
}
