using System.Diagnostics;
using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed class D3D11SpineAnimationPreviewRenderBackend : ISpineAnimationPreviewRenderBackend
{
    private const int BufferCount = 2;
    private const int DxgiErrorDeviceRemoved = unchecked((int)0x887A0005);
    private const int DxgiErrorDeviceHung = unchecked((int)0x887A0006);
    private const int DxgiErrorDeviceReset = unchecked((int)0x887A0007);
    private const int DxgiErrorDriverInternalError = unchecked((int)0x887A0020);

    private readonly SwapChainPanel _panel;
    private readonly D3D11SpineRenderDiagnostics _diagnostics;
    private readonly D3D11SpineTexturePageCache _texturePageCache = new();
    private readonly D3D11SpineAnimationBatchRenderer _batchRenderer = new();
    private readonly SpineAnimationRenderBatchCache _batchCache = new();
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _resizeTimer;
    private SpineAnimationFrameDto? _currentFrame;
    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGISwapChain1? _swapChain;
    private ID3D11RenderTargetView? _renderTargetView;
    private int _pixelWidth;
    private int _pixelHeight;
    private int _textureLoadGeneration;
    private int _texturePageVersion;
    private bool _deviceRecoveryAttempted;

    public D3D11SpineAnimationPreviewRenderBackend(
        SwapChainPanel panel,
        D3D11SpineRenderDiagnostics? diagnostics = null)
    {
        _panel = panel;
        _diagnostics = diagnostics ?? new D3D11SpineRenderDiagnostics(null);
        _panel.SizeChanged += OnPanelSizeChanged;
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

    public void LoadTexturePages(IReadOnlyDictionary<string, byte[]> texturePageContents)
    {
        ArgumentNullException.ThrowIfNull(texturePageContents);

        var generation = ++_textureLoadGeneration;
        _texturePageCache.Clear();
        InvalidateBatchCache();
        if (texturePageContents.Count == 0)
        {
            Invalidate();
            return;
        }

        _ = LoadTexturePagesAsync(texturePageContents, generation);
        Invalidate();
    }

    public void ClearTexturePages()
    {
        _textureLoadGeneration++;
        _texturePageCache.Clear();
        InvalidateBatchCache();
        Invalidate();
    }

    public void ResetFrameCache()
    {
        _batchCache.Clear();
    }

    public void ReleaseResources()
    {
        _resizeTimer.Stop();
        _deviceRecoveryAttempted = false;
        ReleaseDeviceResources();
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
        var batches = ResolveBatches(out var batchProjectionElapsed);
        var clipBatches = ResolveClipBatches(batches, out var clipProjectionElapsed);
        _context.ClearRenderTargetView(_renderTargetView, ResolveSmokeTint(batches.Count));
        var drawElapsed = TimeSpan.Zero;
        if (clipBatches.Count > 0)
        {
            var drawStopwatch = Stopwatch.StartNew();
            _batchRenderer.Draw(
                _device!,
                _context,
                clipBatches,
                _texturePageCache.GpuPages,
                _pixelWidth,
                _pixelHeight);
            drawStopwatch.Stop();
            drawElapsed = drawStopwatch.Elapsed;
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
            ReleaseDeviceResources();
            Invalidate();
            return;
        }

        _diagnostics.RenderingFailed(exception);
        ReleaseDeviceResources();
        RenderingFailed?.Invoke(this, EventArgs.Empty);
    }

    private void ReleaseDeviceResources()
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

    private IReadOnlyList<SpineAnimationClipSpaceBatch> ResolveClipBatches(
        IReadOnlyList<SpineAnimationVertexBatch> batches,
        out TimeSpan elapsed)
    {
        var stopwatch = Stopwatch.StartNew();
        var clipBatches = _batchCache.GetClipBatches(batches, _pixelWidth, _pixelHeight);
        stopwatch.Stop();
        elapsed = stopwatch.Elapsed;
        return clipBatches;
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
                InvalidateBatchCache();
                Invalidate();
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
                    _texturePageCache.Clear();
                    InvalidateBatchCache();
                    Invalidate();
                }
            });
        }
    }

    private D3D11SpineRenderFrameMetrics CreateFrameMetrics(
        IReadOnlyList<SpineAnimationVertexBatch> batches,
        TimeSpan elapsed,
        TimeSpan textureUploadElapsed,
        TimeSpan batchProjectionElapsed,
        TimeSpan clipProjectionElapsed,
        TimeSpan drawElapsed,
        TimeSpan presentElapsed)
    {
        var vertexCount = 0;
        var indexCount = 0;
        var stats = D3D11SpineBatchStats.Empty;
        foreach (var batch in batches)
        {
            vertexCount += batch.Vertices.Count;
            indexCount += batch.Indices.Count;
            stats = stats.Include(batch);
        }

        return new D3D11SpineRenderFrameMetrics(
            batches.Count,
            vertexCount,
            indexCount,
            _texturePageCache.GpuPages.Count,
            _texturePageCache.GpuPageBytes,
            _pixelWidth,
            _pixelHeight,
            elapsed,
            stats.Format(),
            textureUploadElapsed,
            batchProjectionElapsed,
            clipProjectionElapsed,
            drawElapsed,
            presentElapsed);
    }

    private void InvalidateBatchCache()
    {
        _texturePageVersion++;
        _batchCache.Clear();
    }

    private static long CountPayloadBytes(IReadOnlyDictionary<string, byte[]> texturePageContents)
    {
        long payloadBytes = 0;
        foreach (var content in texturePageContents.Values)
        {
            payloadBytes += content.Length;
        }

        return payloadBytes;
    }

    private static string FormatDecodedTextureSummary(
        IReadOnlyDictionary<string, D3D11SpineDecodedTexturePage> decodedPages)
    {
        var stats = D3D11SpineTexturePixelStats.Empty;
        foreach (var page in decodedPages.Values)
        {
            stats = stats.Include(page);
        }

        return stats.Format();
    }

    private static Color4 ResolveSmokeTint(int projectedBatchCount)
    {
        if (projectedBatchCount <= 0)
        {
            return new Color4(0.02f, 0.04f, 0.08f, 1f);
        }

        var batchCount = Math.Clamp(projectedBatchCount, 1, 8);
        return new Color4(0.02f, 0.08f + (batchCount * 0.04f), 0.16f + (batchCount * 0.06f), 1f);
    }

    internal static bool IsRecoverableDeviceLossHResult(int hresult) =>
        hresult is DxgiErrorDeviceRemoved or
            DxgiErrorDeviceHung or
            DxgiErrorDeviceReset or
            DxgiErrorDriverInternalError;

    private readonly record struct D3D11SpineBatchStats(
        bool HasVertices,
        double MinX,
        double MinY,
        double MaxX,
        double MaxY,
        double MinU,
        double MinV,
        double MaxU,
        double MaxV,
        double MinAlpha,
        double MaxAlpha)
    {
        public static D3D11SpineBatchStats Empty =>
            new(false, 0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d);

        public D3D11SpineBatchStats Include(SpineAnimationVertexBatch batch)
        {
            var stats = this;
            foreach (var vertex in batch.Vertices)
            {
                stats = stats.Include(vertex);
            }

            return stats;
        }

        public string Format() =>
            HasVertices
                ? $"bounds={Format(MinX)},{Format(MinY)}..{Format(MaxX)},{Format(MaxY)} " +
                  $"uv={Format(MinU)},{Format(MinV)}..{Format(MaxU)},{Format(MaxV)} " +
                  $"alpha={Format(MinAlpha)}..{Format(MaxAlpha)}"
                : string.Empty;

        private D3D11SpineBatchStats Include(SpineAnimationBatchVertex vertex)
        {
            if (!HasVertices)
            {
                return new D3D11SpineBatchStats(
                    true,
                    vertex.X,
                    vertex.Y,
                    vertex.X,
                    vertex.Y,
                    vertex.U,
                    vertex.V,
                    vertex.U,
                    vertex.V,
                    vertex.A,
                    vertex.A);
            }

            return this with
            {
                MinX = Math.Min(MinX, vertex.X),
                MinY = Math.Min(MinY, vertex.Y),
                MaxX = Math.Max(MaxX, vertex.X),
                MaxY = Math.Max(MaxY, vertex.Y),
                MinU = Math.Min(MinU, vertex.U),
                MinV = Math.Min(MinV, vertex.V),
                MaxU = Math.Max(MaxU, vertex.U),
                MaxV = Math.Max(MaxV, vertex.V),
                MinAlpha = Math.Min(MinAlpha, vertex.A),
                MaxAlpha = Math.Max(MaxAlpha, vertex.A)
            };
        }

        private static string Format(double value) =>
            value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private readonly record struct D3D11SpineTexturePixelStats(
        bool HasPixels,
        int MinAlpha,
        int MaxAlpha,
        long NonZeroAlphaPixels,
        long TotalPixels)
    {
        public static D3D11SpineTexturePixelStats Empty =>
            new(false, 255, 0, 0, 0);

        public D3D11SpineTexturePixelStats Include(D3D11SpineDecodedTexturePage page)
        {
            var stats = this;
            for (var index = 3; index < page.BgraPixels.Length; index += 4)
            {
                stats = stats.Include(page.BgraPixels[index]);
            }

            return stats;
        }

        public string Format()
        {
            if (!HasPixels || TotalPixels <= 0)
            {
                return string.Empty;
            }

            var nonZeroPercent = NonZeroAlphaPixels * 100d / TotalPixels;
            return string.Create(
                CultureInfo.InvariantCulture,
                $"textureAlpha={MinAlpha}..{MaxAlpha} nonZero={nonZeroPercent:0.###}%");
        }

        private D3D11SpineTexturePixelStats Include(byte alpha)
        {
            return new D3D11SpineTexturePixelStats(
                true,
                HasPixels ? Math.Min(MinAlpha, alpha) : alpha,
                HasPixels ? Math.Max(MaxAlpha, alpha) : alpha,
                NonZeroAlphaPixels + (alpha > 0 ? 1 : 0),
                TotalPixels + 1);
        }
    }
}
