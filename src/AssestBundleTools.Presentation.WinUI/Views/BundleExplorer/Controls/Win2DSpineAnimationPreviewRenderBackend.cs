using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Windows.Graphics.DirectX;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed class Win2DSpineAnimationPreviewRenderBackend : ISpineAnimationPreviewRenderBackend
{
    private static readonly IReadOnlyDictionary<string, SpineAnimationTexturePageContent> EmptyTexturePages =
        new Dictionary<string, SpineAnimationTexturePageContent>(StringComparer.Ordinal);

    private readonly CanvasControl _canvas;
    private readonly Dictionary<string, CanvasBitmap> _textureBitmaps = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SpineAnimationTexturePageSize> _texturePageSizes = new(StringComparer.Ordinal);
    private readonly SpineAnimationFrameRenderCache _frameRenderCache = new();
    private readonly SpineAnimationStableAutoFitBounds _stableAutoFitBounds = new();
    private IReadOnlyDictionary<string, SpineAnimationTexturePageContent> _texturePageContents = EmptyTexturePages;
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

    public void SetBackgroundMode(InspectorPreviewImageBackgroundMode backgroundMode)
    {
        // Win2D 预览继续使用 XAML 背景层，D3D 后端才需要自行绘制背景。
    }

    public void PrewarmFrames(IReadOnlyList<SpineAnimationFrameDto> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        if (_stableAutoFitBounds.SetReferenceFrames(frames))
        {
            _frameRenderCache.Clear();
            Invalidate();
        }
    }

    public void LoadTexturePages(IReadOnlyDictionary<string, SpineAnimationTexturePageContent> texturePageContents)
    {
        ArgumentNullException.ThrowIfNull(texturePageContents);

        if (SpineAnimationTexturePageContentComparer.ReferenceEqualsByEntry(_texturePageContents, texturePageContents))
        {
            return;
        }

        _texturePageContents = texturePageContents;
        var generation = ++_textureLoadGeneration;
        _stableAutoFitBounds.ResetBounds();
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
        _stableAutoFitBounds.ResetBounds();
        ClearTextureBitmaps();
        Invalidate();
    }

    public void ResetFrameCache()
    {
        _stableAutoFitBounds.ResetBounds();
        _frameRenderCache.Clear();
    }

    public void ReleaseResources()
    {
        _texturePageContents = EmptyTexturePages;
        ClearTextureBitmaps();
        _currentFrame = null;
        _textureLoadGeneration++;
        _stableAutoFitBounds.ClearReferenceFrames();
        _stableAutoFitBounds.ResetBounds();
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
        IReadOnlyDictionary<string, SpineAnimationTexturePageContent> texturePageContents,
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

                if (pair.Value.ColorContent.Length == 0)
                {
                    continue;
                }

                var decodedPages = await SpineAnimationTexturePageDecoder.DecodeAsync(
                    new Dictionary<string, SpineAnimationTexturePageContent>(StringComparer.Ordinal)
                    {
                        [pair.Key] = pair.Value
                    });
                if (!decodedPages.TryGetValue(pair.Key, out var decodedPage))
                {
                    continue;
                }

                var bitmap = CanvasBitmap.CreateFromBytes(
                    _canvas,
                    decodedPage.BgraPixels,
                    decodedPage.Width,
                    decodedPage.Height,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    96f,
                    CanvasAlphaMode.Premultiplied);
                if (generation != _textureLoadGeneration ||
                    !texturePageContents.TryGetValue(pair.Key, out var currentContent) ||
                    !ReferenceEquals(currentContent.ColorContent, pair.Value.ColorContent) ||
                    !ReferenceEquals(currentContent.AlphaContent, pair.Value.AlphaContent))
                {
                    bitmap.Dispose();
                    continue;
                }

                _textureBitmaps[pair.Key] = bitmap;
                _texturePageSizes[pair.Key] = new SpineAnimationTexturePageSize(
                    decodedPage.Width,
                    decodedPage.Height,
                    decodedPage.AlphaMap);
                _stableAutoFitBounds.ResetBounds();
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

        var stableFitChanged = _stableAutoFitBounds.TryResolveBounds(
            SpineAnimationVertexBatchProjector.Project(_currentFrame, _texturePageSizes),
            ResolveReferenceFrameBatches,
            out var autoFitBounds);
        if (stableFitChanged)
        {
            _frameRenderCache.Clear();
        }

        _frameRenderCache.Draw(
            args.DrawingSession,
            sender,
            _currentFrame,
            _textureBitmaps,
            _texturePageSizes,
            width,
            height,
            _viewport,
            autoFitBounds);
    }

    private IReadOnlyList<SpineAnimationVertexBatch> ResolveReferenceFrameBatches(
        SpineAnimationFrameDto frame) =>
        SpineAnimationVertexBatchProjector.Project(frame, _texturePageSizes);

    private void ClearTextureBitmaps()
    {
        foreach (var bitmap in _textureBitmaps.Values)
        {
            bitmap.Dispose();
        }

        _textureBitmaps.Clear();
        _texturePageSizes.Clear();
        _frameRenderCache.Clear();
    }
}
