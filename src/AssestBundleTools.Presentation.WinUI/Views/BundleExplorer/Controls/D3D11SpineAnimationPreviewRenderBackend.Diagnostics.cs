using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed partial class D3D11SpineAnimationPreviewRenderBackend
{
    private D3D11SpineRenderFrameMetrics CreateFrameMetrics(
        IReadOnlyList<SpineAnimationVertexBatch> batches,
        SpineAnimationAutoFitBounds autoFitBounds,
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
            stats.Format(autoFitBounds),
            textureUploadElapsed,
            batchProjectionElapsed,
            clipProjectionElapsed,
            drawElapsed,
            presentElapsed);
    }

    private static long CountPayloadBytes(IReadOnlyDictionary<string, SpineAnimationTexturePageContent> texturePageContents)
    {
        long payloadBytes = 0;
        foreach (var content in texturePageContents.Values)
        {
            payloadBytes += content.ColorContent.Length;
            payloadBytes += content.AlphaContent?.Length ?? 0;
        }

        return payloadBytes;
    }

    private static string FormatDecodedTextureSummary(
        IReadOnlyDictionary<string, SpineAnimationDecodedTexturePage> decodedPages)
    {
        var stats = D3D11SpineTexturePixelStats.Empty;
        foreach (var page in decodedPages.Values)
        {
            stats = stats.Include(page);
        }

        return stats.Format();
    }

    internal static bool IsRecoverableDeviceLossHResult(int hresult) =>
        hresult is DxgiErrorDeviceRemoved or
            DxgiErrorDeviceHung or
            DxgiErrorDeviceReset or
            DxgiErrorDriverInternalError;
}
