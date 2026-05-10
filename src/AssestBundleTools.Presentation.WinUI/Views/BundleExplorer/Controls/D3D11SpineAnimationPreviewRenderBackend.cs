using System.Diagnostics;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed partial class D3D11SpineAnimationPreviewRenderBackend : ISpineAnimationPreviewRenderBackend
{
    private static readonly IReadOnlyDictionary<string, SpineAnimationTexturePageContent> EmptyTexturePages =
        new Dictionary<string, SpineAnimationTexturePageContent>(StringComparer.Ordinal);

    private const int BufferCount = 2;
    private const int DxgiErrorDeviceRemoved = unchecked((int)0x887A0005);
    private const int DxgiErrorDeviceHung = unchecked((int)0x887A0006);
    private const int DxgiErrorDeviceReset = unchecked((int)0x887A0007);
    private const int DxgiErrorDriverInternalError = unchecked((int)0x887A0020);
    private const int D3DMaxCachedFrames = 128;

    private readonly SwapChainPanel _panel;
    private readonly D3D11SpineRenderDiagnostics _diagnostics;
    private readonly D3D11SpineTexturePageCache _texturePageCache = new();
    private readonly D3D11SpineAnimationBatchRenderer _batchRenderer = new();
    private readonly D3D11SpinePreviewBackgroundRenderer _backgroundRenderer = new();
    private readonly SpineAnimationRenderBatchCache _batchCache = new(D3DMaxCachedFrames);
    private readonly SpineAnimationStableAutoFitBounds _stableAutoFitBounds = new();
    private readonly D3D11SpineBatchPrewarmer _batchPrewarmer;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _resizeTimer;
    private IReadOnlyDictionary<string, SpineAnimationTexturePageContent> _texturePageContents = EmptyTexturePages;
    private SpineAnimationFrameDto? _currentFrame;
    private SpineAnimationPreviewViewport _viewport = SpineAnimationPreviewViewport.Default;
    private InspectorPreviewImageBackgroundMode _backgroundMode = InspectorPreviewImageBackgroundMode.Solid;
    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGISwapChain1? _swapChain;
    private ID3D11RenderTargetView? _renderTargetView;
    private int _pixelWidth;
    private int _pixelHeight;
    private double _compositionScaleX = 1d;
    private double _compositionScaleY = 1d;
    private double _appliedCompositionScaleX;
    private double _appliedCompositionScaleY;
    private int _textureLoadGeneration;
    private int _texturePageVersion;
    private bool _textureLoadPending;
    private bool _deviceRecoveryAttempted;

    public D3D11SpineAnimationPreviewRenderBackend(
        SwapChainPanel panel,
        D3D11SpineRenderDiagnostics? diagnostics = null)
    {
        _panel = panel;
        _diagnostics = diagnostics ?? new D3D11SpineRenderDiagnostics(null);
        _panel.SizeChanged += OnPanelSizeChanged;
        _panel.CompositionScaleChanged += OnPanelCompositionScaleChanged;
        D3D11SpineAnimationBatchRenderer.BeginShaderPrecompile();
        _batchPrewarmer = new D3D11SpineBatchPrewarmer(
            _panel.DispatcherQueue,
            _batchCache,
            _diagnostics,
            () => _currentFrame,
            () => _texturePageVersion,
            Invalidate);
        _resizeTimer = _panel.DispatcherQueue.CreateTimer();
        _resizeTimer.Interval = TimeSpan.FromMilliseconds(80);
        _resizeTimer.IsRepeating = false;
        _resizeTimer.Tick += OnResizeTimerTick;
    }

    public event EventHandler? RenderingFailed;

    public void SetCurrentFrame(SpineAnimationFrameDto? frame)
    {
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
        Invalidate();
    }

    public void SetBackgroundMode(InspectorPreviewImageBackgroundMode backgroundMode)
    {
        if (EqualityComparer<InspectorPreviewImageBackgroundMode>.Default.Equals(_backgroundMode, backgroundMode))
        {
            return;
        }

        _backgroundMode = backgroundMode;
        Invalidate();
    }

    public void PrewarmFrames(IReadOnlyList<SpineAnimationFrameDto> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        var fitBoundsChanged = _stableAutoFitBounds.SetReferenceFrames(frames);
        _batchPrewarmer.SetFrames(frames);
        StartBatchPrewarmIfReady();
        if (fitBoundsChanged)
        {
            Invalidate();
        }
    }

    public void LoadTexturePages(IReadOnlyDictionary<string, SpineAnimationTexturePageContent> texturePageContents)
    {
        ArgumentNullException.ThrowIfNull(texturePageContents);

        if (SpineAnimationTexturePageContentComparer.ReferenceEqualsByEntry(_texturePageContents, texturePageContents) &&
            (_textureLoadPending || _texturePageCache.TexturePageSizes.Count > 0))
        {
            return;
        }

        _texturePageContents = texturePageContents;
        var generation = ++_textureLoadGeneration;
        _texturePageCache.Clear();
        _stableAutoFitBounds.ResetBounds();
        InvalidateBatchCache();
        if (texturePageContents.Count == 0)
        {
            _textureLoadPending = false;
            Invalidate();
            return;
        }

        _textureLoadPending = true;
        _ = LoadTexturePagesAsync(texturePageContents, generation);
        Invalidate();
    }

    public void ClearTexturePages()
    {
        _texturePageContents = EmptyTexturePages;
        _textureLoadPending = false;
        _textureLoadGeneration++;
        _texturePageCache.Clear();
        _stableAutoFitBounds.ResetBounds();
        InvalidateBatchCache();
        Invalidate();
    }

    public void ResetFrameCache()
    {
        _batchCache.Clear();
        _stableAutoFitBounds.ResetBounds();
        StartBatchPrewarmIfReady();
    }

    public void ReleaseResources()
    {
        _resizeTimer.Stop();
        _deviceRecoveryAttempted = false;
        _batchPrewarmer.Cancel();
        ReleaseDeviceResources(clearTextureContents: true);
    }

    public void Invalidate()
    {
        if (_panel.DispatcherQueue.HasThreadAccess)
        {
            Render();
            return;
        }

        _panel.DispatcherQueue.TryEnqueue(Render);
    }

    private void OnPanelSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _resizeTimer.Stop();
        _resizeTimer.Start();
    }

    private void OnPanelCompositionScaleChanged(SwapChainPanel sender, object args)
    {
        _resizeTimer.Stop();
        _resizeTimer.Start();
    }

    private void OnResizeTimerTick(
        Microsoft.UI.Dispatching.DispatcherQueueTimer sender,
        object args)
    {
        Invalidate();
    }

    private void Render()
    {
        try
        {
            RenderCore();
        }
        catch (Exception exception)
        {
            HandleRenderingFailure(exception);
        }
    }

    private void RenderCore()
    {
        var frameStopwatch = Stopwatch.StartNew();
        if (!EnsureDeviceResources() || !ResizeIfNeeded())
        {
            return;
        }

        if (_renderTargetView is null)
        {
            return;
        }

        if (_texturePageCache.TexturePageSizes.Count == 0 &&
            _texturePageContents.Count > 0 &&
            !_textureLoadPending)
        {
            LoadTexturePages(_texturePageContents);
            return;
        }

        _context!.OMSetRenderTargets(_renderTargetView);
        var uploadStopwatch = Stopwatch.StartNew();
        var uploadResult = _texturePageCache.EnsureGpuPages(_device!);
        uploadStopwatch.Stop();
        var uploadElapsed = uploadStopwatch.Elapsed;
        _diagnostics.GpuTextureUploadCompleted(
            uploadResult.UploadedPageCount,
            uploadResult.UploadedBytes,
            _texturePageCache.GpuPages.Count,
            _texturePageCache.GpuPageBytes,
            uploadElapsed);
        if (!TryResolveBatches(out var batches, out var batchProjectionElapsed))
        {
            return;
        }

        var autoFitBounds = ResolveStableAutoFitBounds(batches);
        _backgroundRenderer.Draw(
            _device!,
            _context,
            _renderTargetView,
            _pixelWidth,
            _pixelHeight,
            _compositionScaleX,
            _compositionScaleY,
            _backgroundMode);
        var drawElapsed = TimeSpan.Zero;
        var clipProjectionElapsed = TimeSpan.Zero;
        if (batches.Count > 0)
        {
            var drawStopwatch = Stopwatch.StartNew();
            _batchRenderer.Draw(
                _device!,
                _context,
                batches,
                _texturePageCache.GpuPages,
                _pixelWidth,
                _pixelHeight,
                _viewport.ScalePan(_compositionScaleX, _compositionScaleY),
                autoFitBounds,
                out clipProjectionElapsed);
            drawStopwatch.Stop();
            drawElapsed = drawStopwatch.Elapsed - clipProjectionElapsed;
            if (drawElapsed < TimeSpan.Zero)
            {
                drawElapsed = TimeSpan.Zero;
            }
        }

        var presentStopwatch = Stopwatch.StartNew();
        _swapChain!.Present(1, PresentFlags.None);
        presentStopwatch.Stop();
        frameStopwatch.Stop();
        _deviceRecoveryAttempted = false;
        _diagnostics.FrameRendered(CreateFrameMetrics(
            batches,
            autoFitBounds,
            frameStopwatch.Elapsed,
            uploadElapsed,
            batchProjectionElapsed,
            clipProjectionElapsed,
            drawElapsed,
            presentStopwatch.Elapsed));
    }

    private void HandleRenderingFailure(Exception exception)
    {
        if (!_deviceRecoveryAttempted && IsRecoverableDeviceLossHResult(exception.HResult))
        {
            _deviceRecoveryAttempted = true;
            _diagnostics.DeviceLostRecoveryQueued(exception);
            ReleaseDeviceResources(clearTextureContents: false);
            Invalidate();
            return;
        }

        _diagnostics.RenderingFailed(exception);
        ReleaseDeviceResources(clearTextureContents: false);
        RenderingFailed?.Invoke(this, EventArgs.Empty);
    }

    private void ReleaseDeviceResources(bool clearTextureContents)
    {
        _renderTargetView?.Dispose();
        _swapChain?.Dispose();
        _context?.Dispose();
        _device?.Dispose();
        _renderTargetView = null;
        _swapChain = null;
        _context = null;
        _device = null;
        _pixelWidth = 0;
        _pixelHeight = 0;
        _compositionScaleX = 1d;
        _compositionScaleY = 1d;
        _appliedCompositionScaleX = 0d;
        _appliedCompositionScaleY = 0d;
        _texturePageCache.Clear();
        _backgroundRenderer.ReleaseDeviceResources();
        _batchRenderer.ReleaseDeviceResources();
        _batchCache.Clear();
        _stableAutoFitBounds.ResetBounds();
        _batchPrewarmer.Cancel();
        if (clearTextureContents)
        {
            _texturePageContents = EmptyTexturePages;
            _textureLoadPending = false;
        }
    }

    private IReadOnlyList<SpineAnimationVertexBatch> ResolveBatches(out TimeSpan elapsed)
    {
        var stopwatch = Stopwatch.StartNew();
        var batches = _batchCache.GetVertexBatches(
            _currentFrame,
            _texturePageCache.TexturePageSizes,
            _texturePageVersion);
        stopwatch.Stop();
        elapsed = stopwatch.Elapsed;
        return batches;
    }

    private SpineAnimationAutoFitBounds ResolveStableAutoFitBounds(
        IReadOnlyList<SpineAnimationVertexBatch> currentBatches)
    {
        _stableAutoFitBounds.TryResolveBounds(
            currentBatches,
            ResolveReferenceFrameBatches,
            out var bounds);
        return bounds;
    }

    private IReadOnlyList<SpineAnimationVertexBatch> ResolveReferenceFrameBatches(
        SpineAnimationFrameDto frame) =>
        _batchCache.GetVertexBatches(
            frame,
            _texturePageCache.TexturePageSizes,
            _texturePageVersion);

    private async Task LoadTexturePagesAsync(
        IReadOnlyDictionary<string, SpineAnimationTexturePageContent> texturePageContents,
        int generation)
    {
        var requestedPageCount = texturePageContents.Count;
        var payloadBytes = CountPayloadBytes(texturePageContents);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var decodedPages = await SpineAnimationTexturePageDecoder.DecodeAsync(texturePageContents);
            stopwatch.Stop();
            _diagnostics.TextureDecodeCompleted(
                requestedPageCount,
                decodedPages.Count,
                payloadBytes,
                stopwatch.Elapsed,
                FormatDecodedTextureSummary(decodedPages));
            if (generation != _textureLoadGeneration)
            {
                return;
            }

            _panel.DispatcherQueue.TryEnqueue(() =>
            {
                if (generation != _textureLoadGeneration)
                {
                    return;
                }

                _texturePageCache.ReplaceDecodedPages(decodedPages);
                _textureLoadPending = false;
                InvalidateBatchCache();
                if (!StartBatchPrewarmIfReady())
                {
                    Invalidate();
                }
            });
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            _diagnostics.TextureDecodeFailed(
                requestedPageCount,
                payloadBytes,
                stopwatch.Elapsed,
                exception);
            if (generation != _textureLoadGeneration)
            {
                return;
            }

            _panel.DispatcherQueue.TryEnqueue(() =>
            {
                if (generation == _textureLoadGeneration)
                {
                    _texturePageContents = EmptyTexturePages;
                    _textureLoadPending = false;
                    _texturePageCache.Clear();
                    InvalidateBatchCache();
                    Invalidate();
                }
            });
        }
    }

    private bool TryResolveBatches(
        out IReadOnlyList<SpineAnimationVertexBatch> batches,
        out TimeSpan elapsed)
    {
        if (_batchCache.TryGetVertexBatches(_currentFrame, _texturePageVersion, out batches))
        {
            elapsed = TimeSpan.Zero;
            return true;
        }

        batches = ResolveBatches(out elapsed);
        return true;
    }

    private bool StartBatchPrewarmIfReady() =>
        _batchPrewarmer.TryStart(_texturePageCache.TexturePageSizes, _texturePageVersion);

    private void InvalidateBatchCache()
    {
        _texturePageVersion++;
        _batchCache.Clear();
        _stableAutoFitBounds.ResetBounds();
        _batchPrewarmer.Cancel();
    }

}
