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
}
