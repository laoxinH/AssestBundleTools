using System.Diagnostics;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed partial class D3D11SpineAnimationPreviewRenderBackend
{
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

        if (!ResolvePanelMetrics(out var metrics))
        {
            return false;
        }

        ApplyPanelMetrics(metrics);
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
            AlphaMode = AlphaMode.Premultiplied
        };
        _swapChain = factory.CreateSwapChainForComposition(_device, swapChainDescription);
        WinUI3SwapChainPanelNative.SetSwapChain(_panel, _swapChain, _diagnostics);
        ApplySwapChainCompositionTransform(metrics);
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
        if (_swapChain is null || !ResolvePanelMetrics(out var metrics))
        {
            return false;
        }

        if (metrics.PixelWidth == _pixelWidth &&
            metrics.PixelHeight == _pixelHeight &&
            _renderTargetView is not null)
        {
            ApplyPanelMetrics(metrics);
            ApplySwapChainCompositionTransform(metrics);
            return true;
        }

        ApplyPanelMetrics(metrics);
        _renderTargetView?.Dispose();
        _renderTargetView = null;
        _swapChain.ResizeBuffers(
            BufferCount,
            (uint)_pixelWidth,
            (uint)_pixelHeight,
            Format.B8G8R8A8_UNorm,
            SwapChainFlags.None);
        ApplySwapChainCompositionTransform(metrics);
        CreateRenderTargetView();
        _diagnostics.Resized(_pixelWidth, _pixelHeight);
        return true;
    }

    private bool ResolvePanelMetrics(out D3D11SpinePanelMetrics metrics) =>
        D3D11SpineSwapChainComposition.TryResolvePanelMetrics(
            _panel.ActualWidth,
            _panel.ActualHeight,
            _panel.CompositionScaleX,
            _panel.CompositionScaleY,
            XamlRootScale,
            out metrics);

    private void ApplyPanelMetrics(D3D11SpinePanelMetrics metrics)
    {
        _pixelWidth = metrics.PixelWidth;
        _pixelHeight = metrics.PixelHeight;
        _compositionScaleX = metrics.CompositionScaleX;
        _compositionScaleY = metrics.CompositionScaleY;
    }

    private void ApplySwapChainCompositionTransform(D3D11SpinePanelMetrics metrics)
    {
        if (_swapChain is null ||
            AreClose(_appliedCompositionScaleX, metrics.CompositionScaleX) &&
            AreClose(_appliedCompositionScaleY, metrics.CompositionScaleY))
        {
            return;
        }

        using var swapChain2 = _swapChain.QueryInterface<IDXGISwapChain2>();
        var matrix = D3D11SpineSwapChainComposition.CreateInverseScaleTransform(
            metrics.CompositionScaleX,
            metrics.CompositionScaleY);
        swapChain2.MatrixTransform = matrix;
        _appliedCompositionScaleX = metrics.CompositionScaleX;
        _appliedCompositionScaleY = metrics.CompositionScaleY;
        _diagnostics.SwapChainCompositionTransformApplied(
            metrics.PixelWidth,
            metrics.PixelHeight,
            metrics.CompositionScaleX,
            metrics.CompositionScaleY,
            matrix);
    }

    private double XamlRootScale => _panel.XamlRoot?.RasterizationScale ?? 1d;

    private void CreateRenderTargetView()
    {
        using var backBuffer = _swapChain!.GetBuffer<ID3D11Texture2D>(0);
        _renderTargetView = _device!.CreateRenderTargetView(backBuffer);
    }

    private static bool AreClose(double left, double right) =>
        Math.Abs(left - right) < 0.0000001d;
}
