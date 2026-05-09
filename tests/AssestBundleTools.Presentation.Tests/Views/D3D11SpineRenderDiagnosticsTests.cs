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
}
