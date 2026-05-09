using System.Diagnostics;
using System.Globalization;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed class D3D11SpineRenderDiagnostics
{
    private const int WarmupFrameLogCount = 5;
    private const int SteadyFrameLogInterval = 60;
    private const double SlowFrameThresholdMilliseconds = 33d;
    private readonly Action<string>? _writeMessage;
    private int _frameNumber;

    public D3D11SpineRenderDiagnostics(Action<string>? writeMessage)
    {
        _writeMessage = writeMessage;
    }

    public bool IsEnabled => _writeMessage is not null;

    public void DeviceCreated(int width, int height)
    {
        if (!IsEnabled)
        {
            return;
        }

        Write($"device-created size={width}x{height}");
    }

    public void NativePanelBound(string strategy)
    {
        if (!IsEnabled)
        {
            return;
        }

        Write($"native-panel-bound strategy={strategy}");
    }

    public void NativePanelBindingStarted(
        string panelType,
        bool isLoaded,
        bool hasXamlRoot,
        double actualWidth,
        double actualHeight)
    {
        if (!IsEnabled)
        {
            return;
        }

        Write(
            $"native-panel-binding-started panelType={panelType} loaded={isLoaded} " +
            $"xamlRoot={hasXamlRoot} size={FormatNumber(actualWidth)}x{FormatNumber(actualHeight)}");
    }

    public void NativePanelBindingFailed(string strategy, Exception exception)
    {
        if (!IsEnabled)
        {
            return;
        }

        Write(
            $"native-panel-binding-failed strategy={strategy} exception={exception.GetType().Name} " +
            $"hresult=0x{exception.HResult:X8}");
    }

    public void Resized(int width, int height)
    {
        if (!IsEnabled)
        {
            return;
        }

        Write($"resize size={width}x{height}");
    }

    public void TextureDecodeCompleted(
        int requestedPageCount,
        int decodedPageCount,
        long payloadBytes,
        TimeSpan elapsed,
        string pixelSummary = "")
    {
        if (!IsEnabled)
        {
            return;
        }

        Write(
            $"texture-decode requested={requestedPageCount} decoded={decodedPageCount} " +
            $"payloadBytes={payloadBytes} elapsedMs={FormatMilliseconds(elapsed)}" +
            FormatBatchSummary(pixelSummary));
    }

    public void TextureDecodeFailed(
        int requestedPageCount,
        long payloadBytes,
        TimeSpan elapsed,
        Exception exception)
    {
        if (!IsEnabled)
        {
            return;
        }

        Write(
            $"texture-decode-failed requested={requestedPageCount} payloadBytes={payloadBytes} " +
            $"elapsedMs={FormatMilliseconds(elapsed)} exception={exception.GetType().Name}");
    }

    public void GpuTextureUploadCompleted(
        int uploadedPageCount,
        long uploadedBytes,
        int totalGpuPageCount,
        long totalGpuBytes,
        TimeSpan elapsed)
    {
        if (!IsEnabled || uploadedPageCount <= 0)
        {
            return;
        }

        Write(
            $"gpu-texture-upload uploaded={uploadedPageCount} totalGpuPages={totalGpuPageCount} " +
            $"uploadedBytes={uploadedBytes} totalGpuBytes={totalGpuBytes} elapsedMs={FormatMilliseconds(elapsed)}");
    }

    public void FrameRendered(D3D11SpineRenderFrameMetrics metrics)
    {
        if (!IsEnabled)
        {
            return;
        }

        _frameNumber++;
        if (!ShouldLogFrame(metrics.Elapsed))
        {
            return;
        }

        Write(
            $"frame number={_frameNumber} batches={metrics.BatchCount} vertices={metrics.VertexCount} " +
            $"indices={metrics.IndexCount} gpuPages={metrics.GpuPageCount} gpuBytes={metrics.GpuPageBytes} " +
            $"size={metrics.Width}x{metrics.Height} elapsedMs={FormatMilliseconds(metrics.Elapsed)}" +
            FormatBatchSummary(metrics.BatchSummary));
    }

    public void RenderingFailed(Exception exception)
    {
        if (!IsEnabled)
        {
            return;
        }

        Write(
            $"rendering-failed exception={exception.GetType().Name} " +
            $"hresult=0x{exception.HResult:X8} message={FormatMessage(exception.Message)}");
    }

    private bool ShouldLogFrame(TimeSpan elapsed) =>
        _frameNumber <= WarmupFrameLogCount ||
        _frameNumber % SteadyFrameLogInterval == 0 ||
        elapsed.TotalMilliseconds >= SlowFrameThresholdMilliseconds;

    private void Write(string message)
    {
        _writeMessage?.Invoke($"spine.d3d11 {message}");
    }

    private static string FormatMilliseconds(TimeSpan elapsed) =>
        FormatNumber(elapsed.TotalMilliseconds);

    private static string FormatBatchSummary(string batchSummary) =>
        string.IsNullOrWhiteSpace(batchSummary)
            ? string.Empty
            : $" {batchSummary}";

    private static string FormatNumber(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "<empty>";
        }

        return message
            .ReplaceLineEndings(" ")
            .Replace('\t', ' ')
            .Trim();
    }
}

internal readonly record struct D3D11SpineRenderFrameMetrics(
    int BatchCount,
    int VertexCount,
    int IndexCount,
    int GpuPageCount,
    long GpuPageBytes,
    int Width,
    int Height,
    TimeSpan Elapsed,
    string BatchSummary = "");
