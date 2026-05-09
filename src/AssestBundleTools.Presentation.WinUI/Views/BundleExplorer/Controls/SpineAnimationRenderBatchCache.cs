using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed class SpineAnimationRenderBatchCache
{
    private static readonly IReadOnlyList<SpineAnimationVertexBatch> EmptyVertexBatches =
        Array.Empty<SpineAnimationVertexBatch>();
    private static readonly IReadOnlyList<SpineAnimationClipSpaceBatch> EmptyClipBatches =
        Array.Empty<SpineAnimationClipSpaceBatch>();

    private SpineAnimationFrameDto? _vertexFrame;
    private int _texturePageVersion = -1;
    private IReadOnlyList<SpineAnimationVertexBatch> _vertexBatches = EmptyVertexBatches;
    private IReadOnlyList<SpineAnimationVertexBatch>? _clipSourceBatches;
    private float _clipViewportWidth;
    private float _clipViewportHeight;
    private IReadOnlyList<SpineAnimationClipSpaceBatch> _clipBatches = EmptyClipBatches;

    public IReadOnlyList<SpineAnimationVertexBatch> GetVertexBatches(
        SpineAnimationFrameDto? frame,
        IReadOnlyDictionary<string, SpineAnimationTexturePageSize> texturePages,
        int texturePageVersion)
    {
        ArgumentNullException.ThrowIfNull(texturePages);

        if (frame is null || texturePages.Count == 0)
        {
            Clear();
            return EmptyVertexBatches;
        }

        if (ReferenceEquals(_vertexFrame, frame) &&
            _texturePageVersion == texturePageVersion)
        {
            return _vertexBatches;
        }

        _vertexFrame = frame;
        _texturePageVersion = texturePageVersion;
        _vertexBatches = SpineAnimationVertexBatchProjector.Project(frame, texturePages);
        ClearClipBatches();
        return _vertexBatches;
    }

    public IReadOnlyList<SpineAnimationClipSpaceBatch> GetClipBatches(
        IReadOnlyList<SpineAnimationVertexBatch> batches,
        float viewportWidth,
        float viewportHeight)
    {
        ArgumentNullException.ThrowIfNull(batches);

        if (batches.Count == 0)
        {
            ClearClipBatches();
            return EmptyClipBatches;
        }

        if (ReferenceEquals(_clipSourceBatches, batches) &&
            NearlyEquals(_clipViewportWidth, viewportWidth) &&
            NearlyEquals(_clipViewportHeight, viewportHeight))
        {
            return _clipBatches;
        }

        _clipSourceBatches = batches;
        _clipViewportWidth = viewportWidth;
        _clipViewportHeight = viewportHeight;
        _clipBatches = SpineAnimationClipSpaceProjector.Project(batches, viewportWidth, viewportHeight);
        return _clipBatches;
    }

    public void Clear()
    {
        _vertexFrame = null;
        _texturePageVersion = -1;
        _vertexBatches = EmptyVertexBatches;
        ClearClipBatches();
    }

    private void ClearClipBatches()
    {
        _clipSourceBatches = null;
        _clipViewportWidth = 0f;
        _clipViewportHeight = 0f;
        _clipBatches = EmptyClipBatches;
    }

    private static bool NearlyEquals(float left, float right) =>
        Math.Abs(left - right) < 0.001f;
}
