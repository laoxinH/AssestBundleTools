using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;

/// <summary>
/// Spine runtime adapter 组合点；真实官方 runtime 按版本在此层封装。
/// </summary>
public sealed class SpineAnimationRuntimePreviewService : ISpineAnimationRuntimePreviewService
{
    private readonly IReadOnlyDictionary<string, ISpineAnimationRuntimeAdapter> _adaptersByKey;

    public SpineAnimationRuntimePreviewService()
        : this([new V38SpineAnimationRuntimeAdapter(), new V42SpineAnimationRuntimeAdapter()])
    {
    }

    internal SpineAnimationRuntimePreviewService(IReadOnlyList<ISpineAnimationRuntimeAdapter> adapters)
    {
        _adaptersByKey = adapters.ToDictionary(static adapter => adapter.AdapterKey, StringComparer.Ordinal);
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
}
