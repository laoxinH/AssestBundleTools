using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;

/// <summary>
/// Spine runtime adapter 组合点；真实官方 runtime 按版本在此层封装。
/// </summary>
public sealed class SpineAnimationRuntimePreviewService :
    ISpineAnimationRuntimePreviewService,
    ISpineAnimationRuntimePlaybackService
{
    private readonly IReadOnlyDictionary<string, ISpineAnimationRuntimeAdapter> _adaptersByKey;
    private readonly IReadOnlyDictionary<string, ISpineAnimationRuntimePlaybackAdapter> _playbackAdaptersByKey;

    public SpineAnimationRuntimePreviewService()
        : this([new V38SpineAnimationRuntimeAdapter(), new V42SpineAnimationRuntimeAdapter()])
    {
    }

    internal SpineAnimationRuntimePreviewService(IReadOnlyList<ISpineAnimationRuntimeAdapter> adapters)
    {
        _adaptersByKey = adapters.ToDictionary(static adapter => adapter.AdapterKey, StringComparer.Ordinal);
        _playbackAdaptersByKey = adapters
            .OfType<ISpineAnimationRuntimePlaybackAdapter>()
            .ToDictionary(static adapter => adapter.AdapterKey, StringComparer.Ordinal);
    }

    public Task<SpineAnimationRuntimePreviewResultDto> TryBuildPreviewAsync(
        SpineAnimationRuntimePreviewRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(SpineAnimationRuntimePreviewResultDto.Cancelled);
        }

        var adapterKey = request.Runtime?.AdapterKey;
        if (string.IsNullOrWhiteSpace(adapterKey) ||
            !_adaptersByKey.TryGetValue(adapterKey, out var adapter))
        {
            return Task.FromResult(TryBuildBinaryPreviewWithAnyAdapter(request, cancellationToken));
        }

        return Task.FromResult(adapter.TryBuildPreview(request, cancellationToken));
    }

    private SpineAnimationRuntimePreviewResultDto TryBuildBinaryPreviewWithAnyAdapter(
        SpineAnimationRuntimePreviewRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.SkeletonBinaryContent is not { Length: > 0 })
        {
            return SpineAnimationRuntimePreviewResultDto.NotHandled;
        }

        foreach (var adapter in _adaptersByKey.Values)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return SpineAnimationRuntimePreviewResultDto.Cancelled;
            }

            var result = adapter.TryBuildPreview(request, cancellationToken);
            if (result.IsHandled || result.IsCancelled)
            {
                return result;
            }
        }

        return SpineAnimationRuntimePreviewResultDto.NotHandled;
    }

    public Task<SpineAnimationRuntimePlaybackSessionResultDto> TryCreateSessionAsync(
        SpineAnimationRuntimePlaybackSourceDto source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(SpineAnimationRuntimePlaybackSessionResultDto.Cancelled);
        }

        return Task.Run(
            () => TryCreateSession(source, cancellationToken));
    }

    public Task<SpineAnimationRuntimeFixedFrameSamplingResultDto> SampleFixedFramesAsync(
        SpineAnimationRuntimeFixedFrameSamplingRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Source);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(SpineAnimationRuntimeFixedFrameSamplingResultDto.Cancelled);
        }

        return Task.Run(
            () => SampleFixedFramesCoreAsync(request, cancellationToken));
    }

    public Task<SpineAnimationRuntimeFrameSequenceExportPlanResultDto> PlanFrameSequenceExportAsync(
        SpineAnimationRuntimeFrameSequenceExportPlanRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Source);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(SpineAnimationRuntimeFrameSequenceExportPlanResultDto.Cancelled);
        }

        return Task.Run(
            () => PlanFrameSequenceExportCoreAsync(request, cancellationToken));
    }

    private SpineAnimationRuntimePlaybackSessionResultDto TryCreateSession(
        SpineAnimationRuntimePlaybackSourceDto source,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return SpineAnimationRuntimePlaybackSessionResultDto.Cancelled;
        }

        var adapterKey = source.Runtime?.AdapterKey;
        if (string.IsNullOrWhiteSpace(adapterKey) ||
            !_playbackAdaptersByKey.TryGetValue(adapterKey, out var adapter))
        {
            var binaryResult = TryCreateBinarySessionWithAnyAdapter(source, cancellationToken);
            return binaryResult.IsHandled || binaryResult.IsCancelled
                ? binaryResult
                : TryCreateSampledFallbackSession(source, cancellationToken);
        }

        var adapterResult = adapter.TryCreatePlaybackSession(source, cancellationToken);
        return adapterResult.IsHandled || adapterResult.IsCancelled
            ? adapterResult
            : TryCreateSampledFallbackSession(source, cancellationToken);
    }

    private SpineAnimationRuntimePlaybackSessionResultDto TryCreateBinarySessionWithAnyAdapter(
        SpineAnimationRuntimePlaybackSourceDto source,
        CancellationToken cancellationToken)
    {
        if (source.SkeletonBinaryContent is not { Length: > 0 })
        {
            return SpineAnimationRuntimePlaybackSessionResultDto.NotHandled;
        }

        foreach (var adapter in _playbackAdaptersByKey.Values)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return SpineAnimationRuntimePlaybackSessionResultDto.Cancelled;
            }

            var result = adapter.TryCreatePlaybackSession(source, cancellationToken);
            if (result.IsHandled || result.IsCancelled)
            {
                return result;
            }
        }

        return SpineAnimationRuntimePlaybackSessionResultDto.NotHandled;
    }

    private static SpineAnimationRuntimePlaybackSessionResultDto TryCreateSampledFallbackSession(
        SpineAnimationRuntimePlaybackSourceDto source,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return SpineAnimationRuntimePlaybackSessionResultDto.Cancelled;
        }

        return source.SampledFallback is null
            ? SpineAnimationRuntimePlaybackSessionResultDto.NotHandled
            : SpineAnimationRuntimePlaybackSessionResultDto.Handled(
                new SpineAnimationSampledFallbackPlaybackSession(
                    source.Runtime?.AdapterKey,
                    source.SampledFallback));
    }

    private async Task<SpineAnimationRuntimeFixedFrameSamplingResultDto> SampleFixedFramesCoreAsync(
        SpineAnimationRuntimeFixedFrameSamplingRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.FramesPerSecond <= 0d ||
            double.IsNaN(request.FramesPerSecond) ||
            double.IsInfinity(request.FramesPerSecond))
        {
            return SpineAnimationRuntimeFixedFrameSamplingResultDto.NotHandled;
        }

        var sessionResult = TryCreateSession(request.Source, cancellationToken);
        if (sessionResult.IsCancelled)
        {
            return SpineAnimationRuntimeFixedFrameSamplingResultDto.Cancelled;
        }

        if (!sessionResult.IsHandled ||
            sessionResult.Session is null)
        {
            return SpineAnimationRuntimeFixedFrameSamplingResultDto.NotHandled;
        }

        using var session = sessionResult.Session;
        var firstFrameResult = await session.GetFrameAsync(
            CreateFixedFrameRequest(request, request.StartTime),
            cancellationToken).ConfigureAwait(false);
        if (firstFrameResult.IsCancelled)
        {
            return SpineAnimationRuntimeFixedFrameSamplingResultDto.Cancelled;
        }

        if (!firstFrameResult.IsHandled ||
            firstFrameResult.Frame is null)
        {
            return SpineAnimationRuntimeFixedFrameSamplingResultDto.NotHandled;
        }

        var sampleTimes = SpineFixedFrameSamplingPolicy.CreateSampleTimes(
            firstFrameResult.Duration,
            request.FramesPerSecond,
            request.StartTime,
            request.EndTime,
            request.IncludeFinalFrame);
        var frames = new List<SpineAnimationFrameDto>(sampleTimes.Count);
        for (var index = 0; index < sampleTimes.Count; index++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return SpineAnimationRuntimeFixedFrameSamplingResultDto.Cancelled;
            }

            var sampleTime = sampleTimes[index];
            if (index == 0 &&
                Math.Abs(sampleTime - firstFrameResult.PlaybackTime) <= 0.000001d)
            {
                frames.Add(firstFrameResult.Frame);
                continue;
            }

            var frameResult = await session.GetFrameAsync(
                CreateFixedFrameRequest(request, sampleTime),
                cancellationToken).ConfigureAwait(false);
            if (frameResult.IsCancelled)
            {
                return SpineAnimationRuntimeFixedFrameSamplingResultDto.Cancelled;
            }

            if (!frameResult.IsHandled ||
                frameResult.Frame is null)
            {
                return SpineAnimationRuntimeFixedFrameSamplingResultDto.NotHandled;
            }

            frames.Add(frameResult.Frame);
        }

        return SpineAnimationRuntimeFixedFrameSamplingResultDto.Handled(
            request.FramesPerSecond,
            firstFrameResult.Duration,
            sampleTimes,
            frames,
            sessionResult.Diagnostics);
    }

    private async Task<SpineAnimationRuntimeFrameSequenceExportPlanResultDto> PlanFrameSequenceExportCoreAsync(
        SpineAnimationRuntimeFrameSequenceExportPlanRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!SpineFrameSequenceExportPlanner.IsValidRequest(request))
        {
            return SpineAnimationRuntimeFrameSequenceExportPlanResultDto.NotHandled;
        }

        var samplingResult = await SampleFixedFramesCoreAsync(
            new SpineAnimationRuntimeFixedFrameSamplingRequestDto(
                request.Source,
                request.AnimationName,
                request.SkinName,
                request.FramesPerSecond,
                request.StartTime,
                request.EndTime,
                request.IncludeFinalFrame),
            cancellationToken).ConfigureAwait(false);
        if (samplingResult.IsCancelled)
        {
            return SpineAnimationRuntimeFrameSequenceExportPlanResultDto.Cancelled;
        }

        return samplingResult.IsHandled
            ? SpineFrameSequenceExportPlanner.CreatePlan(request, samplingResult)
            : SpineAnimationRuntimeFrameSequenceExportPlanResultDto.NotHandled;
    }

    private static SpineAnimationRuntimePlaybackFrameRequestDto CreateFixedFrameRequest(
        SpineAnimationRuntimeFixedFrameSamplingRequestDto request,
        double playbackTime) =>
        new(
            request.AnimationName,
            request.SkinName,
            playbackTime,
            ShowSetupPose: false);
}
