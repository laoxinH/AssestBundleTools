using Vortice.Mathematics;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed partial class D3D11SpineAnimationPreviewRenderBackend
{
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
}
