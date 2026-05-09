using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed class SpineAnimationFallbackRenderBackend : ISpineAnimationPreviewRenderBackend
{
    private static readonly IReadOnlyDictionary<string, SpineAnimationTexturePageContent> EmptyTexturePages =
        new Dictionary<string, SpineAnimationTexturePageContent>(StringComparer.Ordinal);

    private readonly ISpineAnimationPreviewRenderBackend _primary;
    private readonly ISpineAnimationPreviewRenderBackend _fallback;
    private readonly Action<bool> _setPrimaryActive;
    private IReadOnlyDictionary<string, SpineAnimationTexturePageContent> _texturePageContents = EmptyTexturePages;
    private SpineAnimationFrameDto? _currentFrame;
    private SpineAnimationPreviewViewport _viewport = SpineAnimationPreviewViewport.Default;
    private bool _isFallbackActive;

    public SpineAnimationFallbackRenderBackend(
        ISpineAnimationPreviewRenderBackend primary,
        ISpineAnimationPreviewRenderBackend fallback,
        Action<bool> setPrimaryActive)
    {
        _primary = primary;
        _fallback = fallback;
        _setPrimaryActive = setPrimaryActive;
        _setPrimaryActive(true);
    }

    public void SetCurrentFrame(SpineAnimationFrameDto? frame)
    {
        _currentFrame = frame;
        ActiveBackend.SetCurrentFrame(frame);
    }

    public void SetViewport(SpineAnimationPreviewViewport viewport)
    {
        _viewport = viewport.Normalize();
        ActiveBackend.SetViewport(_viewport);
    }

    public void PrewarmFrames(IReadOnlyList<SpineAnimationFrameDto> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        ActiveBackend.PrewarmFrames(frames);
    }

    public void LoadTexturePages(IReadOnlyDictionary<string, SpineAnimationTexturePageContent> texturePageContents)
    {
        ArgumentNullException.ThrowIfNull(texturePageContents);

        if (SpineAnimationTexturePageContentComparer.ReferenceEqualsByEntry(_texturePageContents, texturePageContents))
        {
            return;
        }

        _texturePageContents = texturePageContents;
        ActiveBackend.LoadTexturePages(texturePageContents);
    }

    public void ClearTexturePages()
    {
        _texturePageContents = EmptyTexturePages;
        ActiveBackend.ClearTexturePages();
    }

    public void ResetFrameCache() =>
        ActiveBackend.ResetFrameCache();

    public void ReleaseResources()
    {
        _texturePageContents = EmptyTexturePages;
        _currentFrame = null;
        _primary.ReleaseResources();
        _fallback.ReleaseResources();
    }

    public void Invalidate() =>
        ActiveBackend.Invalidate();

    public void SwitchToFallback()
    {
        if (_isFallbackActive)
        {
            return;
        }

        _isFallbackActive = true;
        _setPrimaryActive(false);
        if (_texturePageContents.Count > 0)
        {
            _fallback.LoadTexturePages(_texturePageContents);
        }
        else
        {
            _fallback.ClearTexturePages();
        }

        _fallback.SetCurrentFrame(_currentFrame);
        _fallback.SetViewport(_viewport);
        _fallback.ResetFrameCache();
        _fallback.Invalidate();
    }

    private ISpineAnimationPreviewRenderBackend ActiveBackend =>
        _isFallbackActive ? _fallback : _primary;
}
