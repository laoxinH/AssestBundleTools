using AssestBundleTools.Application.Assets.Dtos;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed class Win2DSpineAnimationPreviewRenderBackend : ISpineAnimationPreviewRenderBackend
{
    private static readonly IReadOnlyDictionary<string, byte[]> EmptyTexturePages =
        new Dictionary<string, byte[]>(StringComparer.Ordinal);

    private readonly CanvasControl _canvas;
    private readonly Dictionary<string, CanvasBitmap> _textureBitmaps = new(StringComparer.Ordinal);
    private readonly SpineAnimationFrameRenderCache _frameRenderCache = new();
    private IReadOnlyDictionary<string, byte[]> _texturePageContents = EmptyTexturePages;
    private SpineAnimationFrameDto? _currentFrame;
    private SpineAnimationPreviewViewport _viewport = SpineAnimationPreviewViewport.Default;
    private int _textureLoadGeneration;

    public Win2DSpineAnimationPreviewRenderBackend(CanvasControl canvas)
    {
        _canvas = canvas;
        _canvas.Draw += OnCanvasDraw;
        _canvas.SizeChanged += OnCanvasSizeChanged;
    }

    public void SetCurrentFrame(SpineAnimationFrameDto? frame)
    {
        if (ReferenceEquals(_currentFrame, frame))
        {
            return;
        }

        _currentFrame = frame;
        Invalidate();
    }

    public void SetViewport(SpineAnimationPreviewViewport viewport)
    {
        var normalized = viewport.Normalize();
        if (EqualityComparer<SpineAnimationPreviewViewport>.Default.Equals(_viewport, normalized))
        {
            return;
        }

        _viewport = normalized;
        _frameRenderCache.Clear();
        Invalidate();
    }

    public void PrewarmFrames(IReadOnlyList<SpineAnimationFrameDto> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
    }

    public void LoadTexturePages(IReadOnlyDictionary<string, byte[]> texturePageContents)
    {
        ArgumentNullException.ThrowIfNull(texturePageContents);

        if (SpineAnimationTexturePageContentComparer.ReferenceEqualsByEntry(_texturePageContents, texturePageContents))
        {
            return;
        }

        _texturePageContents = texturePageContents;
        var generation = ++_textureLoadGeneration;
        ClearTextureBitmaps();
        if (texturePageContents.Count == 0)
        {
            Invalidate();
            return;
        }

        _ = LoadTextureBitmapsAsync(texturePageContents, generation);
    }

    public void ClearTexturePages()
    {
        _texturePageContents = EmptyTexturePages;
        _textureLoadGeneration++;
        ClearTextureBitmaps();
        Invalidate();
    }

    public void ResetFrameCache()
    {
        _frameRenderCache.Clear();
    }

    public void ReleaseResources()
    {
        _texturePageContents = EmptyTexturePages;
        ClearTextureBitmaps();
        _currentFrame = null;
        _textureLoadGeneration++;
    }

    public void Invalidate()
    {
        if (_canvas.DispatcherQueue.HasThreadAccess)
        {
            _canvas.Invalidate();
            return;
        }

        _canvas.DispatcherQueue.TryEnqueue(Invalidate);
    }

    private async Task LoadTextureBitmapsAsync(
        IReadOnlyDictionary<string, byte[]> texturePageContents,
        int generation)
    {
        try
        {
            foreach (var pair in texturePageContents)
            {
                if (generation != _textureLoadGeneration)
                {
                    return;
                }

                if (pair.Value.Length == 0)
                {
                    continue;
                }

                using var stream = await SpineAnimationTextureStreamFactory.CreateAsync(pair.Value);
                var bitmap = await CanvasBitmap.LoadAsync(_canvas, stream);
                if (generation != _textureLoadGeneration ||
                    !texturePageContents.TryGetValue(pair.Key, out var currentContent) ||
                    !ReferenceEquals(currentContent, pair.Value))
                {
                    bitmap.Dispose();
                    continue;
                }

                _textureBitmaps[pair.Key] = bitmap;
                _frameRenderCache.Clear();
            }

            Invalidate();
        }
        catch
        {
            if (generation == _textureLoadGeneration)
            {
                _texturePageContents = EmptyTexturePages;
                ClearTextureBitmaps();
                Invalidate();
            }
        }
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _frameRenderCache.Clear();
        Invalidate();
    }

    private void OnCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (_currentFrame is null || _textureBitmaps.Count == 0)
        {
            return;
        }

        var width = (float)Math.Max(0d, sender.ActualWidth);
        var height = (float)Math.Max(0d, sender.ActualHeight);
        if (width <= 1f || height <= 1f)
        {
            return;
        }

        _frameRenderCache.Draw(
            args.DrawingSession,
            sender,
            _currentFrame,
            _textureBitmaps,
            width,
            height,
            _viewport);
    }

    private void ClearTextureBitmaps()
    {
        foreach (var bitmap in _textureBitmaps.Values)
        {
            bitmap.Dispose();
        }

        _textureBitmaps.Clear();
        _frameRenderCache.Clear();
    }
}
