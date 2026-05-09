using System.Diagnostics;
using AssestBundleTools.Application.Assets.Dtos;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed partial class D3D11SpineAnimationPreviewRenderBackend : ISpineAnimationPreviewRenderBackend
{
    private static readonly IReadOnlyDictionary<string, byte[]> EmptyTexturePages =
        new Dictionary<string, byte[]>(StringComparer.Ordinal);

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
    private readonly SpineAnimationRenderBatchCache _batchCache = new(D3DMaxCachedFrames);
    private readonly D3D11SpineBatchPrewarmer _batchPrewarmer;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _resizeTimer;
    private IReadOnlyDictionary<string, byte[]> _texturePageContents = EmptyTexturePages;
    private SpineAnimationFrameDto? _currentFrame;
    private SpineAnimationPreviewViewport _viewport = SpineAnimationPreviewViewport.Default;
    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGISwapChain1? _swapChain;
    private ID3D11RenderTargetView? _renderTargetView;
    private int _pixelWidth;
    private int _pixelHeight;
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

    public void PrewarmFrames(IReadOnlyList<SpineAnimationFrameDto> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        _batchPrewarmer.SetFrames(frames);
        StartBatchPrewarmIfReady();
    }

    public void LoadTexturePages(IReadOnlyDictionary<string, byte[]> texturePageContents)
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
        InvalidateBatchCache();
        Invalidate();
    }

    public void ResetFrameCache()
    {
        _batchCache.Clear();
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

        _context.ClearRenderTargetView(_renderTargetView, ResolveSmokeTint(batches.Count));
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
                _viewport.ScalePan(XamlRootScale),
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
        _texturePageCache.Clear();
        _batchRenderer.ReleaseDeviceResources();
        _batchCache.Clear();
        _batchPrewarmer.Cancel();
        if (clearTextureContents)
        {
            _texturePageContents = EmptyTexturePages;
            _textureLoadPending = false;
        }
    }

    private bool EnsureDeviceResources()
    {
        if (_device is not null)
        {
            return true;
        }

        if (!_panel.IsLoaded || _panel.XamlRoot is null)
        {
            return false;
        }

        if (!ResolvePanelSize(out _pixelWidth, out _pixelHeight))
        {
            return false;
        }

        var deviceCreationFlags = DeviceCreationFlags.BgraSupport;
        D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            deviceCreationFlags,
            [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0, FeatureLevel.Level_10_1],
            out _device,
            out _,
            out _context).CheckError();

        using var dxgiDevice = _device.QueryInterface<IDXGIDevice>();
        using var adapter = dxgiDevice.GetAdapter();
        using var factory = adapter.GetParent<IDXGIFactory2>();
        var swapChainDescription = new SwapChainDescription1
        {
            Width = (uint)_pixelWidth,
            Height = (uint)_pixelHeight,
            Format = Format.B8G8R8A8_UNorm,
            Stereo = false,
            SampleDescription = new SampleDescription(1, 0),
            BufferUsage = Usage.RenderTargetOutput,
            BufferCount = BufferCount,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipSequential,
            AlphaMode = AlphaMode.Ignore
        };
        _swapChain = factory.CreateSwapChainForComposition(_device, swapChainDescription);
        WinUI3SwapChainPanelNative.SetSwapChain(_panel, _swapChain, _diagnostics);
        CreateRenderTargetView();
        var prewarmStopwatch = Stopwatch.StartNew();
        _batchRenderer.Prewarm(_device);
        prewarmStopwatch.Stop();
        _diagnostics.BatchRendererPrewarmed(prewarmStopwatch.Elapsed);
        _diagnostics.DeviceCreated(_pixelWidth, _pixelHeight);
        return true;
    }

    private bool ResizeIfNeeded()
    {
        if (_swapChain is null || !ResolvePanelSize(out var width, out var height))
        {
            return false;
        }

        if (width == _pixelWidth && height == _pixelHeight && _renderTargetView is not null)
        {
            return true;
        }

        _pixelWidth = width;
        _pixelHeight = height;
        _renderTargetView?.Dispose();
        _renderTargetView = null;
        _swapChain.ResizeBuffers(BufferCount, (uint)_pixelWidth, (uint)_pixelHeight, Format.B8G8R8A8_UNorm, SwapChainFlags.None);
        CreateRenderTargetView();
        _diagnostics.Resized(_pixelWidth, _pixelHeight);
        return true;
    }

    private bool ResolvePanelSize(out int width, out int height)
    {
        width = Math.Max(1, (int)Math.Ceiling(_panel.ActualWidth * XamlRootScale));
        height = Math.Max(1, (int)Math.Ceiling(_panel.ActualHeight * XamlRootScale));
        return width > 1 && height > 1;
    }

    private double XamlRootScale => _panel.XamlRoot?.RasterizationScale ?? 1d;

    private void CreateRenderTargetView()
    {
        using var backBuffer = _swapChain!.GetBuffer<ID3D11Texture2D>(0);
        _renderTargetView = _device!.CreateRenderTargetView(backBuffer);
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

    private async Task LoadTexturePagesAsync(
        IReadOnlyDictionary<string, byte[]> texturePageContents,
        int generation)
    {
        var requestedPageCount = texturePageContents.Count;
        var payloadBytes = CountPayloadBytes(texturePageContents);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var decodedPages = await D3D11SpineTexturePageDecoder.DecodeAsync(texturePageContents);
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
        _batchPrewarmer.Cancel();
    }

}
