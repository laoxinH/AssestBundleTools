using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed class SpineAnimationStableAutoFitBounds
{
    private static readonly IReadOnlyList<SpineAnimationFrameDto> EmptyReferenceFrames =
        Array.Empty<SpineAnimationFrameDto>();

    private IReadOnlyList<SpineAnimationFrameDto> _referenceFrames = EmptyReferenceFrames;
    private SpineAnimationAutoFitBounds _bounds;

    public SpineAnimationAutoFitBounds Bounds => _bounds;

    public bool SetReferenceFrames(IReadOnlyList<SpineAnimationFrameDto> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        if (HasSameFrameReferences(_referenceFrames, frames))
        {
            return false;
        }

        _referenceFrames = frames.Count == 0 ? EmptyReferenceFrames : frames.ToArray();
        ResetBounds();
        return true;
    }

    public bool ClearReferenceFrames()
    {
        if (_referenceFrames.Count == 0)
        {
            return false;
        }

        _referenceFrames = EmptyReferenceFrames;
        ResetBounds();
        return true;
    }

    public bool ResetBounds()
    {
        if (!_bounds.IsValid)
        {
            return false;
        }

        _bounds = default;
        return true;
    }

    public bool TryResolveBounds(
        IReadOnlyList<SpineAnimationVertexBatch> currentBatches,
        Func<SpineAnimationFrameDto, IReadOnlyList<SpineAnimationVertexBatch>> resolveFrameBatches,
        out SpineAnimationAutoFitBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(currentBatches);
        ArgumentNullException.ThrowIfNull(resolveFrameBatches);

        var previous = _bounds;
        if (!_bounds.IsValid)
        {
            var referenceBounds = ResolveReferenceBounds(resolveFrameBatches);
            _bounds = referenceBounds.IsValid
                ? referenceBounds
                : SpineAnimationAutoFitBoundsResolver.Resolve(currentBatches);
        }

        bounds = _bounds;
        return !EqualityComparer<SpineAnimationAutoFitBounds>.Default.Equals(previous, _bounds);
    }

    private SpineAnimationAutoFitBounds ResolveReferenceBounds(
        Func<SpineAnimationFrameDto, IReadOnlyList<SpineAnimationVertexBatch>> resolveFrameBatches)
    {
        var bounds = default(SpineAnimationAutoFitBounds);
        foreach (var frame in _referenceFrames)
        {
            bounds = SpineAnimationAutoFitBoundsResolver.Union(
                bounds,
                SpineAnimationAutoFitBoundsResolver.Resolve(resolveFrameBatches(frame)));
        }

        return bounds;
    }

    private static bool HasSameFrameReferences(
        IReadOnlyList<SpineAnimationFrameDto> current,
        IReadOnlyList<SpineAnimationFrameDto> next)
    {
        if (ReferenceEquals(current, next))
        {
            return true;
        }

        if (current.Count != next.Count)
        {
            return false;
        }

        for (var index = 0; index < current.Count; index++)
        {
            if (!ReferenceEquals(current[index], next[index]))
            {
                return false;
            }
        }

        return true;
    }
}
