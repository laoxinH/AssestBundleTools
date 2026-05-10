using System.Numerics;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class D3D11SpineRenderDiagnosticsTests
{
    [Fact]
    public void FrameRendered_ShouldLogWarmupFramesAndThrottleSteadyFrames()
    {
        var messages = new List<string>();
        var diagnostics = new D3D11SpineRenderDiagnostics(messages.Add);
        var metrics = new D3D11SpineRenderFrameMetrics(
            BatchCount: 2,
            VertexCount: 8,
            IndexCount: 12,
            GpuPageCount: 1,
            GpuPageBytes: 4096,
            Width: 800,
            Height: 600,
            TimeSpan.FromMilliseconds(4));

        for (var index = 0; index < 60; index++)
        {
            diagnostics.FrameRendered(metrics);
        }

        Assert.Equal(6, messages.Count);
        Assert.Contains("frame number=1", messages[0], StringComparison.Ordinal);
        Assert.Contains("frame number=60", messages[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void FrameRendered_ShouldLogSlowFrameOutsideThrottleWindow()
    {
        var messages = new List<string>();
        var diagnostics = new D3D11SpineRenderDiagnostics(messages.Add);
        var fastMetrics = CreateMetrics(TimeSpan.FromMilliseconds(4));
        var slowMetrics = CreateMetrics(TimeSpan.FromMilliseconds(40));

        for (var index = 0; index < 10; index++)
        {
            diagnostics.FrameRendered(fastMetrics);
        }

        diagnostics.FrameRendered(slowMetrics);

        Assert.Contains(messages, static message => message.Contains("frame number=11", StringComparison.Ordinal));
    }

    [Fact]
    public void FrameRendered_ShouldAppendBatchSummary_WhenProvided()
    {
        var messages = new List<string>();
        var diagnostics = new D3D11SpineRenderDiagnostics(messages.Add);
        var metrics = new D3D11SpineRenderFrameMetrics(
            BatchCount: 1,
            VertexCount: 3,
            IndexCount: 3,
            GpuPageCount: 1,
            GpuPageBytes: 4096,
            Width: 320,
            Height: 240,
            Elapsed: TimeSpan.FromMilliseconds(4),
            BatchSummary: "bounds=-1,-2..3,4 uv=0,0..1,1 alpha=1..1");

        diagnostics.FrameRendered(metrics);

        var message = Assert.Single(messages);
        Assert.Contains("bounds=-1,-2..3,4", message, StringComparison.Ordinal);
        Assert.Contains("alpha=1..1", message, StringComparison.Ordinal);
    }

    [Fact]
    public void FrameRendered_ShouldAppendPipelineTimings_WhenProvided()
    {
        var messages = new List<string>();
        var diagnostics = new D3D11SpineRenderDiagnostics(messages.Add);
        var metrics = new D3D11SpineRenderFrameMetrics(
            BatchCount: 1,
            VertexCount: 3,
            IndexCount: 3,
            GpuPageCount: 1,
            GpuPageBytes: 4096,
            Width: 320,
            Height: 240,
            Elapsed: TimeSpan.FromMilliseconds(12),
            TextureUploadElapsed: TimeSpan.FromMilliseconds(1),
            BatchProjectionElapsed: TimeSpan.FromMilliseconds(2),
            ClipProjectionElapsed: TimeSpan.FromMilliseconds(2.5d),
            DrawElapsed: TimeSpan.FromMilliseconds(3),
            PresentElapsed: TimeSpan.FromMilliseconds(4));

        diagnostics.FrameRendered(metrics);

        var message = Assert.Single(messages);
        Assert.Contains("uploadMs=1", message, StringComparison.Ordinal);
        Assert.Contains("projectMs=2", message, StringComparison.Ordinal);
        Assert.Contains("clipMs=2.5", message, StringComparison.Ordinal);
        Assert.Contains("drawMs=3", message, StringComparison.Ordinal);
        Assert.Contains("presentMs=4", message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlaybackInteraction_ShouldLogSelectionAndPlaybackState()
    {
        var messages = new List<string>();
        var diagnostics = new D3D11SpineRenderDiagnostics(messages.Add);

        diagnostics.PlaybackInteraction(
            "skin-changed",
            "idle",
            "skin-alt",
            isPlaying: true,
            playbackTime: 1.25d,
            playbackSpeed: 2d);

        var message = Assert.Single(messages);
        Assert.Contains("interaction event=skin-changed", message, StringComparison.Ordinal);
        Assert.Contains("animation=idle", message, StringComparison.Ordinal);
        Assert.Contains("skin=skin-alt", message, StringComparison.Ordinal);
        Assert.Contains("playing=True", message, StringComparison.Ordinal);
        Assert.Contains("time=1.25", message, StringComparison.Ordinal);
        Assert.Contains("speed=2", message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextureDecodeCompleted_ShouldIncludePageCountsAndPayloadSize()
    {
        var messages = new List<string>();
        var diagnostics = new D3D11SpineRenderDiagnostics(messages.Add);

        diagnostics.TextureDecodeCompleted(
            2,
            1,
            1234,
            TimeSpan.FromMilliseconds(5),
            "textureAlpha=0..255 nonZero=12.5%");

        var message = Assert.Single(messages);
        Assert.Contains("texture-decode requested=2 decoded=1 payloadBytes=1234", message, StringComparison.Ordinal);
        Assert.Contains("textureAlpha=0..255 nonZero=12.5%", message, StringComparison.Ordinal);
    }

    [Fact]
    public void NativePanelBound_ShouldIncludeStrategy()
    {
        var messages = new List<string>();
        var diagnostics = new D3D11SpineRenderDiagnostics(messages.Add);

        diagnostics.NativePanelBound("winrt-native-object");

        var message = Assert.Single(messages);
        Assert.Contains("native-panel-bound strategy=winrt-native-object", message, StringComparison.Ordinal);
    }

    [Fact]
    public void NativePanelBindingStarted_ShouldIncludePanelReadiness()
    {
        var messages = new List<string>();
        var diagnostics = new D3D11SpineRenderDiagnostics(messages.Add);

        diagnostics.NativePanelBindingStarted(
            "Microsoft.UI.Xaml.Controls.SwapChainPanel",
            isLoaded: true,
            hasXamlRoot: true,
            actualWidth: 320,
            actualHeight: 240);

        var message = Assert.Single(messages);
        Assert.Contains("native-panel-binding-started", message, StringComparison.Ordinal);
        Assert.Contains("loaded=True xamlRoot=True size=320x240", message, StringComparison.Ordinal);
    }

    [Fact]
    public void SwapChainCompositionTransformApplied_ShouldIncludeScaleAndMatrix()
    {
        var messages = new List<string>();
        var diagnostics = new D3D11SpineRenderDiagnostics(messages.Add);

        diagnostics.SwapChainCompositionTransformApplied(
            width: 393,
            height: 203,
            scaleX: 1.25d,
            scaleY: 1.25d,
            Matrix3x2.CreateScale(0.8f));

        var message = Assert.Single(messages);
        Assert.Contains("swapchain-composition size=393x203", message, StringComparison.Ordinal);
        Assert.Contains("scale=1.25x1.25", message, StringComparison.Ordinal);
        Assert.Contains("matrix=0.8,0,0,0.8", message, StringComparison.Ordinal);
    }

    [Fact]
    public void NativePanelBindingFailed_ShouldIncludeStrategyAndHResult()
    {
        var messages = new List<string>();
        var diagnostics = new D3D11SpineRenderDiagnostics(messages.Add);

        diagnostics.NativePanelBindingFailed("marshal-interface", new InvalidCastException());

        var message = Assert.Single(messages);
        Assert.Contains("native-panel-binding-failed strategy=marshal-interface", message, StringComparison.Ordinal);
        Assert.Contains("hresult=0x", message, StringComparison.Ordinal);
    }

    [Fact]
    public void GpuTextureUploadCompleted_ShouldIncludeEstimatedGpuBytes()
    {
        var messages = new List<string>();
        var diagnostics = new D3D11SpineRenderDiagnostics(messages.Add);

        diagnostics.GpuTextureUploadCompleted(1, 4096, 2, 8192, TimeSpan.FromMilliseconds(3));

        var message = Assert.Single(messages);
        Assert.Contains("gpu-texture-upload uploaded=1 totalGpuPages=2", message, StringComparison.Ordinal);
        Assert.Contains("uploadedBytes=4096 totalGpuBytes=8192", message, StringComparison.Ordinal);
    }

    [Fact]
    public void GpuTextureUploadCompleted_ShouldSkipEmptyUpload()
    {
        var messages = new List<string>();
        var diagnostics = new D3D11SpineRenderDiagnostics(messages.Add);

        diagnostics.GpuTextureUploadCompleted(0, 0, 1, 4096, TimeSpan.Zero);

        Assert.Empty(messages);
    }

    [Fact]
    public void BatchRendererPrewarmed_ShouldLogElapsedTime()
    {
        var messages = new List<string>();
        var diagnostics = new D3D11SpineRenderDiagnostics(messages.Add);

        diagnostics.BatchRendererPrewarmed(TimeSpan.FromMilliseconds(7.5d));

        var message = Assert.Single(messages);
        Assert.Contains("batch-renderer-prewarmed elapsedMs=7.5", message, StringComparison.Ordinal);
    }

    [Fact]
    public void BatchPrewarmCompleted_ShouldLogFrameCountsAndElapsedTime()
    {
        var messages = new List<string>();
        var diagnostics = new D3D11SpineRenderDiagnostics(messages.Add);

        diagnostics.BatchPrewarmCompleted(3, 5, TimeSpan.FromMilliseconds(11.25d));

        var message = Assert.Single(messages);
        Assert.Contains("batch-prewarm-completed prewarmedFrames=3 requestedFrames=5", message, StringComparison.Ordinal);
        Assert.Contains("elapsedMs=11.25", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Diagnostics_ShouldStaySilent_WhenNoSinkIsProvided()
    {
        var diagnostics = new D3D11SpineRenderDiagnostics(null);

        diagnostics.DeviceCreated(320, 240);
        diagnostics.GpuTextureUploadCompleted(1, 4, 1, 4, TimeSpan.FromMilliseconds(1));
        diagnostics.FrameRendered(CreateMetrics(TimeSpan.FromMilliseconds(40)));

        Assert.False(diagnostics.IsEnabled);
    }

    [Fact]
    public void RenderingFailed_ShouldIncludeExceptionTypeAndHResult()
    {
        var messages = new List<string>();
        var diagnostics = new D3D11SpineRenderDiagnostics(messages.Add);

        diagnostics.RenderingFailed(new InvalidCastException());

        var message = Assert.Single(messages);
        Assert.Contains("rendering-failed exception=InvalidCastException", message, StringComparison.Ordinal);
        Assert.Contains("hresult=0x", message, StringComparison.Ordinal);
        Assert.Contains("message=", message, StringComparison.Ordinal);
    }

    [Fact]
    public void DeviceLostRecoveryQueued_ShouldIncludeExceptionTypeAndHResult()
    {
        var messages = new List<string>();
        var diagnostics = new D3D11SpineRenderDiagnostics(messages.Add);

        diagnostics.DeviceLostRecoveryQueued(new HResultException(-2005270523));

        var message = Assert.Single(messages);
        Assert.Contains("device-lost recovery=queued", message, StringComparison.Ordinal);
        Assert.Contains("exception=HResultException", message, StringComparison.Ordinal);
        Assert.Contains("hresult=0x887A0005", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-2005270523)]
    [InlineData(-2005270522)]
    [InlineData(-2005270521)]
    [InlineData(-2005270496)]
    public void IsRecoverableDeviceLossHResult_ShouldRecognizeDxgiDeviceLoss(int hresult)
    {
        Assert.True(D3D11SpineAnimationPreviewRenderBackend.IsRecoverableDeviceLossHResult(hresult));
    }

    [Fact]
    public void IsRecoverableDeviceLossHResult_ShouldRejectGenericFailures()
    {
        Assert.False(D3D11SpineAnimationPreviewRenderBackend.IsRecoverableDeviceLossHResult(-2147467259));
    }

    private static D3D11SpineRenderFrameMetrics CreateMetrics(TimeSpan elapsed) =>
        new(
            BatchCount: 1,
            VertexCount: 4,
            IndexCount: 6,
            GpuPageCount: 1,
            GpuPageBytes: 16,
            Width: 320,
            Height: 240,
            elapsed);

    private sealed class HResultException : Exception
    {
        public HResultException(int hresult)
        {
            HResult = hresult;
        }
    }
}
